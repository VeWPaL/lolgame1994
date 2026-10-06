/* 25b-items-roster - the first dozen items Every item here has to answer one question: [h:25b-items-roster-1] */

Items.define('heavy_hands',{
  name:'Heavy Hands', use:'passive', slot:'sigil', rarity:'common', tags:['damage'],
  glyph:'HH', color:'#b98a55',
  blurb:'+1 Strength. Flat, so it adds to every hit instead of scaling the curve that gives each gun its shape.',
  fx:{stats:{strength:1}}
});

Items.define('weighted_rod',{
  name:'Weighted Rod', use:'passive', slot:'sigil', rarity:'common', tags:['damage'],
  glyph:'WR', color:'#8a6238',
  blurb:'+2 Strength. Two of these and a lunger dies in three hits instead of six, which is a different fight.',
  fx:{stats:{strength:2}}
});

Items.define('tin_cup',{
  name:'Tin Cup', use:'active', charges:3, rarity:'common', tags:['survival'],
  glyph:'TC', color:'#c9d3e0',
  blurb:'Q. Drink: two hearts back. Three in it, so it is a resource you spend on a bad room rather than a spare.',
  fx:{hooks:{heal_self:2}}
});

Items.define('bone_whistle',{
  name:'Bone Whistle', use:'active', charges:Infinity, rarity:'uncommon', tags:['utility'],
  glyph:'BW', color:'#d8cfae',
  blurb:'Q. Calls every loose pickup in the room to you. Never runs out, so it is about where you use it.',
  fx:{hooks:{pull_pickups:1}}
});

Items.define('iron_ribs',{
  name:'Iron Ribs', use:'passive', slot:'sigil', rarity:'common', tags:['survival'],
  glyph:'IR', color:'#7f8a99',
  blurb:'+2 Vigor. Two more hearts to lose, which is a slower fight rather than an easier one.',
  fx:{stats:{vigor:2}}
});

Items.define('swift_boots',{
  name:'Swift Boots', use:'passive', slot:'sigil', rarity:'common', tags:['movement'],
  glyph:'SB', color:'#6fbf9a',
  blurb:'+5% top speed. Capped, and shared with Momentum, so it competes with playing well rather than adding to it.',
  fx:{stats:{speed:0.05}}
});

Items.define('lucky_coin',{
  name:'Lucky Coin', use:'passive', slot:'sigil', rarity:'uncommon', tags:['fortune'],
  glyph:'LC', color:'#e0c07a',
  blurb:'+1 Luck. Shifts what the dungeon offers you toward the unusual, and nothing else.',
  fx:{stats:{luck:1}}
});

/* Precision is its own stat, so the item that feeds it is its own item. When the cone rode on Luck,
   Lucky Coin was quietly also a damage item, and the two could not be tuned apart. */
Items.define('steady_hand',{
  name:'Steady Hand', use:'passive', slot:'sigil', rarity:'common', tags:['aim'],
  glyph:'SH', color:'#b8c4d4',
  blurb:'+1 Precision. Narrows the Arcane Beam a fifth. Five of them and the beam is a line.',
  fx:{stats:{precision:1}}
});

Items.define('weighted_grip',{
  name:'Weighted Grip', use:'passive', slot:'sigil', rarity:'rare', tags:['aim'],
  glyph:'WG', color:'#8fa6c4',
  blurb:'+2 Precision. Two fifths off the beam in one pickup, which is most of what a laser costs.',
  fx:{stats:{precision:2}}
});

Items.define('hunters_mark',{
  name:"Hunter's Mark", use:'active', charges:2, rarity:'rare', tags:['utility','fortune'],
  glyph:'HM', color:'#c79bff',
  /* "+1 Luck, permanently" was a promise the item could not keep. [h:25b-items-roster-2] */
  blurb:'Q. Wakes the room and shows what is on the floor. Also +1 Luck for as long as you carry it.',
  fx:{stats:{luck:1},hooks:{reveal_room:1}}
});

/* The first item with a cost, and the reason the roster is not just a list of plusses. [h:25b-items-roster-3] */
Items.define('glass_wands',{
  name:'Glass Wands', use:'passive', slot:'sigil', rarity:'legendary', tags:['damage','brittle'],
  glyph:'GW', color:'#9fe8ff',
  blurb:'+3 Strength, -1 Luck. The strongest thing here, and it takes the stat that makes everything else luckier.',
  fx:{stats:{strength:3,luck:-1}}
});

/* An artifact, which is not a type but a field. It carries unlocks, so it is an artifact AND a
   passive, and either half can be true without the other being a category. */
Items.define('brass_compass',{
  name:'Brass Compass', use:'passive', slot:'sigil', rarity:'legendary', tags:['relic'],
  glyph:'BC', color:'#d6a44a',
  blurb:'A relic. It points somewhere useful, and it is the reason that somewhere exists.',
  unlocks:['compass'],
  fx:{stats:{luck:1}}
});

/* A companion, written down before it is built. The hook exists and does nothing but count, so this
   definition is legal, appears in the pool, and will do the honest empty thing until the behaviour
   lands. That is the seam working as intended rather than a placeholder pretending otherwise. */
Items.define('lantern_friend',{
  name:'Lantern Friend', use:'active', charges:1, rarity:'rare', tags:['companion'],
  glyph:'LF', color:'#f0c86a',
  /* `unimplemented` MARKS THIS AS WRITTEN BUT NOT BUILT, and it is the reason this item is not loot. [h:25b-items-roster-4] */
  blurb:'Q. Sets a lantern down. It is not a creature yet.',
  unimplemented:true,
  fx:{hooks:{spawn_companion:1}}
});
