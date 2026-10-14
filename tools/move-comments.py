"""Move long C# comments out of the code (CONVENTIONS: comments are one or two lines; long reasoning lives in
docs/history/). Every comment block of MIN_LINES or more lines is appended verbatim to
docs/history/csharp/<File>.md under a new [h:<File>-N] heading, and the code keeps a one-line gist with the tag.
XML doc blocks stay XML: the gist becomes a one-line <summary>. Trailing comments on code lines are left alone.

    python tools/move-comments.py [--check] [paths...]   (default: every .cs under csharp/ and unity/Assets/Depths)

--check only reports what would move and exits 1 if anything would (for the gate). Idempotent: a gist line is
one line, so a second run moves nothing.
"""
import html, os, re, sys

MIN_LINES = 3
GIST_MAX = 110
ROOTS = ['csharp/Depths.Core', 'csharp/Depths.Playtest', 'csharp/Depths.Tests', 'unity/Assets/Depths']
HIST = 'docs/history/csharp'


def sources(paths):
    for root in paths:
        if root.endswith('.cs'):
            yield root
            continue
        for dp, dn, fn in os.walk(root):
            dn[:] = sorted(x for x in dn if not x.endswith('~') and x not in ('bin', 'obj'))
            for f in sorted(fn):
                if f.endswith('.cs'):
                    yield os.path.join(dp, f).replace('\\', '/')


def kind(line):
    s = line.strip()
    if s.startswith('///'): return 'doc'
    if s.startswith('//'): return 'line'
    if s.startswith('/*'): return 'block'
    return None


def blocks(lines):
    """(start, end_exclusive, kind) for every run of whole-line comments."""
    i, n = 0, len(lines)
    while i < n:
        k = kind(lines[i])
        if k is None:
            i += 1
            continue
        j = i
        if k == 'block':
            while j < n and '*/' not in lines[j]: j += 1
            j += 1
            # a block comment that closes and then has code on the same line is not a whole-line comment
            if j <= n and lines[j - 1].split('*/', 1)[-1].strip():
                i = j
                continue
        else:
            while j < n and kind(lines[j]) == k: j += 1
        yield i, j, k
        i = j


def plain(text_lines, k):
    out = []
    for l in text_lines:
        s = l.strip()
        if k == 'doc': s = s[3:]
        elif k == 'line': s = s[2:]
        else: s = re.sub(r'^/\*+', '', s); s = re.sub(r'\*+/$', '', s); s = re.sub(r'^\*+', '', s)
        out.append(s.strip())
    t = ' '.join(x for x in out if x)
    t = re.sub(r'<see\s+cref="([^"]+)"\s*/>', lambda m: m.group(1).split('.')[-1].replace('()', ''), t)
    t = re.sub(r'<paramref\s+name="([^"]+)"\s*/>', r'\1', t)
    t = re.sub(r'</?[a-zA-Z][^>]*>', '', t)
    t = html.unescape(t)
    return re.sub(r'\s+', ' ', t).strip()


def gist(text):
    m = re.search(r'(.+?[.!?])(\s|$)', text)
    g = m.group(1) if m and len(m.group(1)) >= 20 else text
    if len(g) > GIST_MAX:
        g = g[:GIST_MAX].rsplit(' ', 1)[0].rstrip(',;:-(') + '...'
    return g


def key_for(path, taken):
    base = os.path.splitext(os.path.basename(path))[0]
    if path.startswith('unity/'): base = 'Unity' + base if base in taken else base
    return base


def main():
    os.chdir(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))   # paths are repo-relative
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    check = '--check' in sys.argv
    files = list(sources(args or ROOTS))
    core_names = {os.path.splitext(os.path.basename(f))[0] for f in files if not f.startswith('unity/')}
    moved_total = 0
    for path in files:
        src = open(path, encoding='utf-8').read()
        lines = src.split('\n')
        found = [(a, b, k) for a, b, k in blocks(lines) if b - a >= MIN_LINES]
        if not found: continue
        moved_total += len(found)
        if check:
            print('%s: %d block(s) of %d+ lines' % (path, len(found), MIN_LINES))
            continue
        key = key_for(path, core_names)
        hist = os.path.join(HIST, key + '.md')
        old = open(hist, encoding='utf-8').read() if os.path.exists(hist) else '# %s - moved comments\n\nLong comments moved out of `%s`. The code keeps a one-line gist tagged `[h:%s-N]`.\n' % (os.path.basename(path), path, key)
        nums = [int(x) for x in re.findall(r'\[h:%s-(\d+)\]' % re.escape(key), old)]
        nums += [int(x) for x in re.findall(r'\[h:%s-(\d+)\]' % re.escape(key), src)]
        n = max(nums or [0])
        out, notes, last = [], [], 0
        for a, b, k in found:
            out.extend(lines[last:a])
            n += 1
            tag = '[h:%s-%d]' % (key, n)
            body = lines[a:b]
            indent = re.match(r'\s*', body[0]).group(0)
            text = plain(body, k)
            g = gist(text) if text else 'moved'
            near = next((l.strip() for l in lines[b:b + 6] if l.strip() and not kind(l)), '')
            if k == 'doc':
                out.append('%s/// <summary>%s %s</summary>' % (indent, html.escape(g, quote=False), tag))
            else:
                out.append('%s// %s %s' % (indent, g, tag))
            dedent = [l[len(indent):] if l.startswith(indent) else l.lstrip() for l in body]
            notes.append('\n## %s\nnear: `%s`\n\n%s\n' % (tag, near[:120], '\n'.join(dedent)))
            last = b
        out.extend(lines[last:])
        open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(out))
        os.makedirs(HIST, exist_ok=True)
        open(hist, 'w', encoding='utf-8', newline='\n').write(old.rstrip('\n') + '\n' + ''.join(notes))
        print('%s: moved %d block(s) to %s' % (path, len(found), hist))
    if check:
        print('%d block(s) would move' % moved_total)
        sys.exit(1 if moved_total else 0)


if __name__ == '__main__':
    main()
