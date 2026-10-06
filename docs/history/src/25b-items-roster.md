# 25b-items-roster.js — moved comments

Long comments moved out of `src/25b-items-roster.js`. The code keeps a one-line gist tagged `[h:25b-items-roster-N]`; search this file for that tag.

## [h:25b-items-roster-1]
near: `Items.define('heavy_hands',{`

25b-items-roster  -  the first dozen items

  Every item here has to answer one question: what does it make the player DO differently? An item
  whose whole effect is a number nobody thinks about is tax, and a sheet full of tax is a sheet
  nobody reads. So the roster is deliberately lopsided - a few plain stat items to teach the shape,
  and then the ones that bend a verb.

  Note what is NOT here yet, and why: no companions, no weapon mods with real behaviour, no
  intelligence doors. All three have an axis reserved and a name that validates, so they can be
  written down now and filled in later without reshaping anything. Building the framework so the
  content can arrive out of order is the entire point of it.

## [h:25b-items-roster-2]
near: `blurb:'Q. Wakes the room and shows what is on the floor. Also +1 Luck for as long as you carry it.',`

"+1 Luck, permanently" was a promise the item could not keep.

  The +1 comes from the item itself, and the item is removed when its two charges are spent - so the
  Luck went with it. Measured: luck 0, pick it up, 1; use both charges, 0. An item that says
  "permanently" and then stops is worse than one that never claimed it, because the player makes a
  build decision on the word.

  The word now describes what happens: the Luck lasts as long as you carry it. If it is ever meant to
  be truly permanent that needs a real permanent-stat path - one that survives the item - and this
  text is where the change belongs, not a comment.

## [h:25b-items-roster-3]
near: `Items.define('glass_wands',{`

The first item with a cost, and the reason the roster is not just a list of plusses. A build that
  is good at everything is a build with no decision in it, and the decision is what makes twenty
  items feel like two hundred. Glass Wands is the best damage in the game and it costs you the
  stat that makes loot and the beam better - so taking it is taking a narrower path.

## [h:25b-items-roster-4]
near: `blurb:'Q. Sets a lantern down. It is not a creature yet.',`

`unimplemented` MARKS THIS AS WRITTEN BUT NOT BUILT, and it is the reason this item is not loot.

    `spawn_companion` exists and returns false, so the definition validates, the item appears on the
    sheet, and pressing Q costs a charge and reports "nothing happens when you use that yet". It was
    offered 26 times in 600 rolls, displacing whatever working active the player was carrying.

    The flag lives on the definition rather than in the pool's filter, so an item cannot be forgotten:
    write a real `spawn_companion`, delete the flag, and the Lantern Friend is loot again.
