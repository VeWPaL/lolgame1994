# 15-content.js — moved comments

Long comments moved out of `src/15-content.js`. The code keeps a one-line gist tagged `[h:15-content-N]`; search this file for that tag.

## [h:15-content-1]
near: `const HOOKS={};`

15-content  -  the content registry

  Everything the game can be built out of is addressed by a stable string id and lives in one
  place, so that adding content stops being a code change.

  WHY THIS EXISTS. The replayability ceiling on this game is not the move set - four guns and two
  right-clicks is a good move set, and small is correct. The ceiling is that there is nothing
  LAYERED over the moves. Every one of the games in this genre is a small move set under a large
  modifier system, and that is where the hours come from. So items are the feature, and a mod is
  just content the author did not ship.

  Both are only cheap if content is DATA. If an item is a class with behaviour in it, a hundred
  items is a hundred files and a mod needs a compiler. So the rule is: a definition is plain data -
  numbers, strings, flags, and NAMED HOOKS - and a hook is a name resolved through the table above.
  A mod can then ship {"stats":{"damage":0.2}} and get working behaviour with no code at all, or
  name a hook that already exists and get the complicated thing for free.

  THE EXISTING TABLES STAY EXACTLY WHERE THEY ARE. ENEMY is an object and WEAPONS is an ARRAY
  indexed by player.weaponIdx, and roughly fifteen call sites depend on those shapes. The registry
  adopts them as its built-in storage rather than replacing them, which keeps this diff reviewable
  and keeps a mod from having to understand how the game was written. What it buys is one place
  that can enumerate, validate, overlay and reload.

## [h:15-content-2]
near: `const AREAS={`

THE FOUR THEMED BLOCKS OF THE CLIMB, as content.

  The palette in 10-art decides what an area looks like and `areaForFloor()` in core decides which
  area a floor is in; what is missing between them is the NAME, and a place with a colour and no
  name is a filter rather than a place. These four ids are exactly the four strings areaForFloor()
  returns, which is the point - the words, the colours and the enemy mix are three answers to one
  question, asked through one spelling of it.

  FLAVOUR IS A LINE, NOT A PARAGRAPH, because this is read on a pause screen. It says what the
  place IS - what makes it worth walking into rather than what happened there - and it is deliberately
  short enough that it cannot push the stat rows off a 560px card.

  Nothing here changes difficulty. An area's identity is its enemy mix plus its palette, and this
  file only supplies the third thing that makes those legible.

## [h:15-content-3]
near: `kinds[name].pristine=isArray?table.map(clone):clone(table);`

A snapshot of the shipped content, taken once, at load. A mod may OVERRIDE a built-in, and
      removing the mods has to put the original BACK - not merely delete the override and leave a
      hole where a body used to be. The first version had no snapshot, so resetMods deleted the
      patched enemy outright and every test after the mod suite died on an undefined body.
      A mod layer that cannot undo itself is not a mod layer.

## [h:15-content-4]
near: `kind('area',AREAS,['name','flavour']);`

AREAS, and the reason they are a kind rather than two constants near the palette.

    An area is identity, and identity is exactly what this registry is for: a name and a line about
    the place, addressed by the same string `areaForFloor()` already returns, so there is no second
    vocabulary for "which area am I in". Adding a fifth area becomes one entry here and one row in
    the palette in 10-art - no code, which is the entire promise the registry makes.

    Required fields are `name` and `flavour`, so an entry that ships without them fails
    validate() rather than printing "undefined" on the character sheet. The four shipped ids are
    the four `areaForFloor()` can return; a mod may add more, and the palette is what decides what
    a missing palette does (it throws loudly, by name - see paletteForArea).

## [h:15-content-5]
near: `function each(name,fn){`

One iteration shape for both storage shapes. Two of the three kinds are objects and one is an
    array, and the first version of this file assumed an array in two places - which meant
    validate() threw on every object kind, and resetMods silently cleaned nothing at all, because
    `table.length` is undefined on an object and the loop simply did not run. A cleanup that quietly
    does nothing is worse than one that fails, because it reports success.

## [h:15-content-6]
near: `if(!fromMod) k.pristine[id]=clone(def);`

The pristine copy is the SHIPPED definition, and it is captured HERE rather than when the kind
      was declared. The item kind is declared as an empty object and filled in by the roster in a
      file loaded later, so a snapshot taken at declaration time was {} - and resetMods() then
      faithfully restored every kind to its pristine state and DELETED THE ENTIRE ROSTER. The mod
      suite calls resetMods(), so the first mod test quietly emptied the item table and every item
      test after it failed on "no item called heavy_hands".

      Only GAME content updates the snapshot. A mod overriding a built-in is origin mod, and must
      leave the original underneath it - which is the whole reason the snapshot exists.
