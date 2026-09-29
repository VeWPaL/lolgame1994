/* ==============================================================================================
   80-ui  -  boot, input, and the DOM overlays

   Also replaced by Unity, but one idea is worth carrying across: the uiHoldsInput / uiAllows pair,
   which is a whole input-suppression layer. Overlays must take the keyboard away from the game and
   hand it back cleanly on close, and that problem does not get smaller because the renderer
   changed - it gets worse, because Unity has its own event systems underneath.
   ============================================================================================== */
mouse={x:W/2,y:H/2}; mouseDown=false; altMouseDown=false; keys={}; state='start';
loadRecords();

// Pressed buttons live in a set rather than two booleans, and every route by which a mouseup can
// be swallowed (native context menu, pointercancel, blur, tab switch, mouseup outside the page)
// is treated as "all released". Two booleans can disagree: if one mouseup goes missing the other
// button stays latched, which is what used to leave the wand firing after letting go of both
// buttons in the same instant. The set also makes the release order irrelevant.
const held=new Set();
function releaseButtons(){held.clear();mouseDown=false;altMouseDown=false;}
function trackButton(b,down){
  if(down) held.add(b); else held.delete(b);
  mouseDown=held.has(0); altMouseDown=held.has(2);
}


/* Every bug this build has had fixed, grouped, with the one-line reason it is pinned at all.
   Kept as a table keyed by test name rather than as an argument on every call site: the call sites
   are the assertions, and burying a description inside them made them harder to read, not easier.
   Anything missing from this table falls into "unclassified" and is visible in the panel, so a new
   bug fix cannot quietly end up uncategorised. */
const FIXES={
  /* ---- frame and input ---- */
  'fixed timestep: 2s of wall clock runs the same ticks at 30 to 240Hz':['frame and input','the simulation advanced a different number of times depending on the monitor, so the same fight was a different fight'],
  'fixed timestep: a jittery 60Hz timer keeps a steady 3.5 updates per frame':['frame and input','a stuttering timer used to make the whole game stutter with it'],
  'fixed timestep: a 5s hitch replays at most 250ms':['frame and input','one slow frame used to teleport every enemy across the room at once'],
  'pause: Esc and P toggle it and nothing advances while paused':['frame and input','enemies kept walking while the game was paused'],
  'pause: a click resumes without casting':['frame and input','un-pausing with the mouse fired the wand as a side effect'],
  'pause: blur and hidden tab release held input and pause':['frame and input','alt-tabbing away left the keys held down and the run playing itself'],
  'pause: not available outside a run':['frame and input','the pause key did nothing on the title screen, which read as a dropped input'],
  'R restarts only while paused or after a run':['frame and input','R wiped a live run by accident'],
  'Space blinks like Shift':['frame and input','the second blink key was documented and not implemented'],
  'releasing both buttons at once cannot leave the wand firing':['frame and input','a fast click-and-drag latched the wand on permanently'],
  'mouse maps to canvas pixels inside the 2px border':['frame and input','the aim was offset by the frame thickness, so shots landed next to the cursor'],

  /* ---- weapons and damage ---- */
  'weapons: every gun kills a chaser fast at the range it is meant to be used at':['weapons and damage','every gun used to be usable everywhere, which is the same as no gun being right anywhere'],
  'every gun loses damage with range but stays worth using':['weapons and damage','a gun that does not fall off is a gun with no range, and a room has one'],
  'weapons actually do less damage to a far target':['weapons and damage','the falloff was drawn on paper and not actually in the damage'],
  'a hit slows the body and the HP increase pays for it':['weapons and damage','hitting something was pure upside, so there was never a reason not to shoot'],
  'the blast shares one damage budget between everyone it catches':['weapons and damage','a crowd was killed by a flat split, so more bodies meant more damage instead of less'],
  'the blast shoves hard up close and barely at all across the room':['weapons and damage','the shove was flat, so the blast was repositioning you the same way everywhere'],
  'the blast wounds the smallest Brunch group instead of deleting it':['weapons and damage','one click deleted a 4-pack, which is the most common thing in the game'],
  'right-click blast detonates on the first body it touches, no phasing':['weapons and damage','the blast flew through bodies to the cursor, so the weapon removed nothing'],
  'the blast detonates on the first body it touches, and the hook does not':['weapons and damage','a tie-break meant for piercing bolts was applied to the blast, which made it phase through the whole room'],
  'a bolt is resolved from the weapon it was thrown with':['weapons and damage','swapping right-clicks mid-flight made a hook detonate as a blast, shoving where it should have pulled'],
  'the voidball drills through a line of bodies and is the worst gun against one of them':['weapons and damage','the Voidball was a marginally better Bolt with no other identity, so there was no reason to pick it'],
  'the pierce discount follows the order the bolt actually reached bodies':['weapons and damage','the discount followed array order, so a line took less damage at the front than the back, invisibly'],
  'the bolt visibly spends itself as it travels, and only visually':['weapons and damage','the falloff was invisible until it landed, which is a number the player has to memorise'],
  'every weapon, spell and icon is the colour its own projectile is':['weapons and damage','the icon lookup defaulted to the blast, so the Bolt was drawn orange while the wand was violet'],
  'a hit tints an enemy instead of painting it white':['weapons and damage','a flashing enemy lost its own colours for a frame, which reads as a rendering fault'],
  'a shell is a chip off a chaser and knocks a body back without launching it':['weapons and damage','a shell could fling a chaser off the map at close range'],

  /* ---- the right click ---- */
  'the hook goes off exactly where the cursor was, deals nothing, and yanks bodies in':['the right click','the hook fired from a stale cursor position'],
  'the hook gathers what it caught into a knot, and it is what waits behind the fake wall':['the right click','a flat pull strength sent bodies through the point and barely moved ones caught close, so the crowd scattered instead of knotting'],
  'the hook leaves a ground spell that holds, drains and grinds':['the right click','a drag that scatters the crowd and lets it walk straight back to you buys nothing'],
  'a second right click detonates the hook in flight, and cannot be farmed':['the right click','you could not pull early, and a held button blew the hook up at your own feet'],
  'the hook cancels on a right click WHILE it is still on cooldown':['the right click','the cancel was behind the cooldown gate, and the hook cooldown outlives a long cast, so the button was on cooldown for most of the flight and the cancel silently did nothing'],
  'the shockwave wears the right colour and travels the way the weapon acts':['the right click','both right-clicks used the same orange expanding ring, so the hook looked like it was shoving'],
  'the secret is walled off, off the map, and only the right click opens it':['the right click','the secret could be reached and was visible before it was earned'],

  /* ---- enemies and AI ---- */
  'a chaser lunges, and it can be dodged by reacting to the tell':['enemies and AI','chasers homed on you directly, so counterstrafing - the answer to the gunners - was the worst possible move against them'],
  'a hook cancels a lunge outright, and stars mark the bodies it holds':['enemies and AI','a stun skipped the state machine without clearing it, so the glow stayed up, the aim line stayed drawn, and the attack you had already dodged landed anyway'],
  'a gunner telegraphs before it fires, and the tell previews the shot':['enemies and AI','a shell left the instant the cooldown ran out, so the only counter to a gunner was not being there - and the flash meant to warn you was drawn in the projectile loop, where a gunner never appears, so it was never visible at all'],
  'a gunner is slower and heavier than a shooter, and pays for it':['enemies and AI','the two gunners were the same gun at two sizes, so there was no reason to want the big one'],
  'Brunch: tiny, quick, in a knot, and the big packs are the rare ones':['enemies and AI','pack size was uniform, so a room could roll eight Brunch as often as four'],
  'a Brunch spends itself on touching you, and a pack eats itself':['enemies and AI','a knot of Brunch was a damage clock that never stopped, and i-frames shielded the pack from ever paying for a hit'],
  'the gunner is tougher than a shooter and slips, but only from range':['enemies and AI','the gunner was a slower, bigger shooter, so it had no answer of its own'],
  'the gunner is the shooter cloned: slower, double damage, bigger':['enemies and AI','the two gunners had drifted into the same body with different numbers'],
  'the gunners notice you well before they will shoot, and fight at close quarters':['enemies and AI','gunners shot from across the room at anything that moved'],
  'a hit wakes the room from any distance':['enemies and AI','a body you hit across the room did not know it had been hit'],
  'chasers that reach the player keep their bodies apart':['enemies and AI','a whole pack could stack on one pixel and land as a single hit'],
  'separation keeps a chaser pinned in a corner inside the room':['enemies and AI','pushing bodies apart could shove one out through a wall'],
  'the spawn plan spreads bodies out and keeps gunners off the entry door':['enemies and AI','bodies spawned in a knot or in the doorway, so the room decided the fight before you moved'],
  'counterstrafing beats a straight line, and a straight line is punished':['enemies and AI','the gunners led their shots perfectly, so moving well made their aim better and there was no way to dodge a straight line'],

  /* ---- movement and being hit ---- */
  'being hit is where the model is, and it is not the whole model':['movement and being hit','the hitbox sat across the hood and stopped at the waist, so you could not be hit for anything that reached your legs'],
  'a blink leaves the enemy targeting the old spot for a moment':['movement and being hit','a gunner that was already tracking you got a free intercept shot the instant you vanished'],
  'a blink is invulnerable for the whole move, not just the first frame of it':['movement and being hit','the invulnerability window started and finished inside the jump, so the half of the blink that actually happens afterwards was fair game'],
  'blink: charges come back faster, and faster still in a quiet room':['movement and being hit','blink charges only came back on a slow timer, so the escape tool ran out mid-fight'],
  'clearing a room still sprints the blink bar for the walk out':['movement and being hit','walking to the next room cost you the dodge you needed for it'],
  'walking into a live room hands back the whole kit, once per room':['movement and being hit','re-entering a fight could refill everything at once, so the room could be reset by stepping out and back'],
  'losing your last point of health ends the run, and nothing on the floor can save it':['movement and being hit','dying could be undone by a heart on the floor, so the health bar was not the source of truth'],
  'a zero-health player is never still playing, whatever the tick was doing':['movement and being hit','an early return could skip the death check, leaving a dead player running'],
  'fractional damage can never leave you alive on an empty health bar':['movement and being hit','repeated fractional subtraction landed health on 6.66e-16, which is not zero, so the death check never fired'],

  /* ---- rooms, keys and getting further ---- */
  'every dungeon: four winding runs, each ending on a reward':['rooms and progression','the map had loops in it, so a run could be all backtracking and no depth'],
  'the critical path is walkable: silver to the upgrade, gold to the boss':['rooms and progression','a generator could produce a floor whose key room could not be reached'],
  'the two keys gate different doors and are each spent once':['rooms and progression','one key could open both doors, so the progression was one gate with two props'],
  'clearing the two key rooms pays out the right metal':['rooms and progression','the two key rooms paid the same metal, so the boss door could open before the upgrade room did'],
  'a touch of the door starts the unlock and it finishes on its own':['rooms and progression','standing on the door did nothing, so unlocking needed a frame-perfect click'],
  'item room: two different weapons, never the one you hold':['rooms and progression','the upgrade room could offer the gun you already had, which is a dead pickup'],
  'weapon pickup swaps, and waits for you to step off':['rooms and progression','picking up a weapon could ping-pong forever with the one you dropped'],
  'hearts and armor stay on the floor when you are full':['rooms and progression','a full player deleted a heart instead of leaving it'],
  'blast kills go through killEnemy (kill count and loot)':['rooms and progression','kills were counted in one place and loot dropped in another, so the two disagreed'],
  'killing the boss opens a way out instead of ending the run':['rooms and progression','the results screen was up before you could decide whether the run was over'],
  'the boss warns you once, when it first shows on the map':['rooms and progression','the boss warning was re-shown for every room on the path to it'],

  /* ---- HUD and interface ---- */
  'the HUD is laid out as one block, and every plate is derived from the same margins':['HUD and interface','every plate was hand-placed against its neighbour, so raising max hp left the key plate floating in the middle of nowhere'],
  'the bug list opens on a key, in a normal game, with no query string':['HUD and interface','the change history was locked behind a test URL, so the one reader who needs it during a normal game is the one who cannot reach it'],
  'the controls sheet and the bug list take the keyboard away from the game':['HUD and interface','a DOM overlay on a canvas game is a click that casts and a key that walks you into a Brunch - and a suppressor that takes too much swallows the key that closes the overlay, which is a cancel you cannot reach, the same bug the hook cooldown gate was'],
  'a gunner shoots the gap it found, not the gap it wanted':['enemies and AI','the aim search sweeps a cone and returns the angle it cleared, but only its truthiness was being read - so whenever the straight line was blocked and a few degrees off was not, the gunner charged the blocked line and put the shell through the very Brunch it had just avoided'],
  'a lunge that cannot reach waits, and one that can is never dawdled with':['enemies and AI','distance is a reason the attack has not started, not a reason it fails; a chaser that commits past its own reach reads as the attack breaking for no reason the player can see'],
  'a chaser holds its distance instead of walking into you, and lunges across the gap':['enemies and AI','a chaser with no standoff closed the last thirty pixels and then lunged from zero range, where no read is worth anything because there is nothing left to dodge - every measurement of this attack came out a hundred percent for that reason, and not because the prediction was good'],
  'a pack of chasers arrives around you, not in a line':['enemies and AI','every chaser steered at the player exact position, so a pack came as one front - one line, one angle, one threat to read. The bodies now hold slots on a ring, spread by the golden angle so no two ever share one'],
  'two chasers that lunge into each other both come off worse':['enemies and AI','a lunge is aimed at a position, so two bodies reading the same player in the same instant were aimed at the same point and flew through each other; putting yourself between them is a real play and it now pays'],
  'a gunner in your face hits a straight line AND a counterstrafer, and only far away misses':['enemies and AI','counterstrafing in front of a gunner was free, and the reason was arithmetic rather than tuning. The aim was not a lead at all - it was a claim about where the player would be, fired AT a shell that only covers so much ground, so every shot missed by the margin of the claim. The half second of visible charging was not counted either, so the gunner predicted a target that had already moved. And it solved all of this from where it stood when the muzzle lit up, while walking a hundred and twenty pixels during the charge it had just announced. It now solves a real intercept, from a muzzle that holds still to charge, at the part of the player the hit test actually uses'],
  'the hook is devastating once and a nuisance the third time, and bodies forget':['the right click','the hook was a permanent answer - land it, wait out the field, land it again, and a body never recovered. The resistance is per body and it decays, so the first hook is untouched and that keeps it the answer to a lunge'],
  'a lunge reads a settled heading, and a thrashing one is read as unreliable':['enemies and AI','aiming from the raw velocity aims at a player one tick into a keypress, who is still nearly stationary, and at a player mid-reversal, who is momentarily pointing the wrong way'],
  'a lunge is aimed at where you are going, and running in a straight line is a hit':['enemies and AI','the lunge led by thirty-two pixels a shot that needed two hundred and ninety-four, and stopped at a hundred and sixty-six - so it fell a hundred and twenty-eight pixels short every single time. Running in a straight line was not a risk to be answered, it was a guaranteed escape, and no amount of reaction could change it'],
  'a lunge cannot be dodged by ignoring it, and can be dodged by answering it':['enemies and AI','fixing the intercept without checking the reaction window would have swapped a free escape for an unavoidable hit, which is the same unreadable threat pointing the other way'],
  'the blink lands with momentum, and only along the way it went':['movement and being hit','the burst was a level bleeding a fraction per tick, so it lasted ten seconds instead of the tenth of one the comment beside it promised - and a flat gain meant you could blink away and immediately walk back at full speed, which is a free displacement rather than momentum'],
  'HUD draws one heart per 2 max hp, and always the full set of armour slots':['HUD and interface','the health plate grew with health but the slots were placed by hand'],
  'both keys always sit in the HUD, dimmed while unheld':['HUD and interface','a key you were not carrying was simply absent, which read as a broken frame'],
  'the minimap reveals only rooms a door actually leads to':['HUD and interface','the map showed rooms that were merely touching you on the grid, with no way in'],
  'an uncleared boss room is marked on the map, and a cleared one is not':['HUD and interface','the boss mark was left behind after the fight was over'],
  'time and heart formatting':['HUD and interface','a formatted value could print 0:60.0 or a negative heart'],

  /* ---- runs, records and the frame ---- */
  'records: a win is saved, a slower win keeps the fastest time':['runs and records','a slow run overwrote the best time, or a win never saved at all'],
  'records: dying on the boss-kill frame is a death, and deaths save rooms explored':['runs and records','a tie on the last frame was resolved the wrong way, so a death was scored as a win'],
  'run stats count shots, hits, kills, damage and time exactly':['runs and records','the numbers on the results screen did not match what the game did'],
  'room fade: entering with enemies eases in across the whole ready window':['presentation','the fade was computed as the complement of a smoothstep, which is flat at both ends and cut to black on the first tick'],
  'room fade: a cleared room snaps back in and a doorway eases to black first':['presentation','rooms faded in at different rates depending on whether they were cleared'],
  'room entry fades in over the ready window and locks the enemies out':['presentation','enemies could act during the entry fade, before the player could see them'],
  'every screen renders without throwing':['presentation','a screen that has not been opened in a build throws the first time it is reached'],
};


/* The bug list. Lives in the MAIN script, not the test one, because it is not a test - it is the
   change history, and a player who has run into something odd should be able to open it and see
   whether it is a known problem without installing a query string.

   `showBugPanel(results)` takes the test results when they exist. With no results it still lists
   every entry, marked fixed, which is the honest state: the table IS the record of what has been
   fixed, and the results only say whether the current build still holds. */
/* ---- DOM overlays and the input they take away ---------------------------------------
   The bug list and the controls sheet are real DOM on top of a canvas game, which means a click
   meant for a button is also a click that reaches the window and casts, and a key pressed while
   reading them walks the player into a Brunch. Suppressing that at each call site means every
   future listener has to remember to; suppressing it once, here, means it cannot be forgotten.

   Two halves, and the second is the one that matters. A CAPTURE-phase listener on window runs
   before the game's BUBBLE-phase listeners on the same node, so stopping propagation there stops
   the event ever reaching them - which covers the listeners nobody remembered. But capture-phase
   suppression alone would still let a key that was already held keep the player walking, and would
   leave the wand firing if the button was down before the sheet opened, so the game's own handlers
   ALSO ask uiHoldsInput() first. Belt and braces, and the second half is what stops the game being
   mid-swing when the overlay closes. */
function uiOverlay(){
  const s=document.getElementById('ctlSheet'), b=document.getElementById('bugPanel'),
        d=document.getElementById('seedSheet');
  if(s&&s.classList.contains('on')) return s;
  if(d&&d.classList.contains('on')) return d;
  if(b&&b.style.display==='block') return b;
  return null;
}
/* True while the keystroke is aimed at the seed field. The suppressor below runs in the CAPTURE
   phase on window, which is ahead of every target in the tree - so stopPropagation there stops a
   key reaching the input at all, and a text field whose every keystroke is eaten accepts nothing,
   silently. The field has to be let through before the suppressor decides anything. */
const seedTyping=e=>{const t=e.target;return !!(t&&t.id==='seedInput');};
function uiHoldsInput(){ return !!uiOverlay(); }
/* The keys the overlays answer to. They have to get past the suppressor below, or the bug list
   cannot be closed with the key that opened it - the same class of bug as a cancel you cannot
   reach, and exactly the mistake the hook's cooldown gate used to be. */
const UI_KEYS=['escape','h','b','s'];
function uiAllows(k){ return UI_KEYS.indexOf(String(k).toLowerCase())>=0; }
// Anything aimed at the overlay is consumed on the way down, before the game sees it.
window.addEventListener('keydown',e=>{
  if(seedTyping(e)) return;
  if(uiHoldsInput()&&!uiAllows(e.key)){ e.preventDefault(); e.stopPropagation(); }
},true);
window.addEventListener('keyup',e=>{
  if(seedTyping(e)) return;
  if(uiHoldsInput()&&!uiAllows(e.key)){ e.preventDefault(); e.stopPropagation(); }
},true);
for(const type of ['mousedown','mouseup','mousemove','contextmenu','wheel','pointercancel']){
  window.addEventListener(type,e=>{
    if(!uiOverlay()) return;
    e.preventDefault(); e.stopPropagation();
  },true);
}
/* And the game lets go of anything it was already holding, once, on the way in. Holding a
   direction into a pause and then closing the pause used to leave the player walking. */
function uiTakeInput(){
  if(!uiHoldsInput()) return;
  keys={}; releaseButtons();
  mouseDown=false; altMouseDown=false;
}
function toggleControls(){
  const s=document.getElementById('ctlSheet');
  if(!s) return;
  s.classList.toggle('on');
  if(s.classList.contains('on')) uiTakeInput();
  else uiReleaseFocus();
}
/* Escape closes whatever is on top, sheet first, and only then the bug list. One key that means
   "get me out of here" rather than one key per overlay, because a player reaching for Escape is not
   thinking about which of the two they opened. */
function uiCloseTop(){
  const s=document.getElementById('ctlSheet');
  if(s&&s.classList.contains('on')){ s.classList.remove('on'); uiReleaseFocus(); return; }
  const d=document.getElementById('seedSheet');
  if(d&&d.classList.contains('on')){ d.classList.remove('on'); uiReleaseFocus(); return; }
  const p=document.getElementById('bugPanel');
  if(p&&p.style.display==='block'){ p.style.display='none'; releaseButtons(); keys={}; return; }
  toggleControls();
}

/* ---- the seed sheet ----------------------------------------------------------------------------
   Opened on S from the title screen or from a pause. No persistence, deliberately: the run summary
   already prints the seed a run used, so the loop is "read it off the sheet, type it here". Quietly
   reloading the previous seed would instead mean two people opening the game saw a different number
   for reasons neither of them could see, which is the exact confusion seeds are supposed to remove.

   An EMPTY field means "surprise me", not "seed zero" - and zero is a perfectly good seed that a
   player can type deliberately, so the two must not collide. */
function seedClose(){
  const d=document.getElementById('seedSheet');
  if(d) d.classList.remove('on');
  uiReleaseFocus();
}
function toggleSeedSheet(){
  const d=document.getElementById('seedSheet');
  if(!d) return;
  if(d.classList.contains('on')){ seedClose(); return; }
  d.classList.add('on');
  uiTakeInput();
  const inp=document.getElementById('seedInput');
  const err=document.getElementById('seedErr');
  if(err) err.textContent='';
  if(inp){ inp.value=Rnd.seedText; setTimeout(()=>{inp.focus();inp.select();},0); }
}
function seedDescend(){
  const inp=document.getElementById('seedInput'), err=document.getElementById('seedErr');
  const raw=(inp&&inp.value||'').trim();
  let n;
  if(!raw) n=Rnd.fresh();
  else{
    n=Rnd.decode(raw);
    if(n===null){
      // refused LOUDLY and in place, keeping what was typed. Silently starting a random dungeon
      // because a letter was mistyped is the one outcome that makes this feel broken.
      if(err) err.textContent='That is not a seed. Seven letters and digits, like '+Rnd.seedText+'.';
      if(inp){ inp.focus(); inp.select(); }
      return;
    }
  }
  Rnd.set(n);
  seedClose();
  startGame();
}
document.getElementById('seedGo').addEventListener('click',seedDescend);
document.getElementById('seedRand').addEventListener('click',()=>{
  Rnd.set(Rnd.fresh());
  const inp=document.getElementById('seedInput'), err=document.getElementById('seedErr');
  if(inp) inp.value=Rnd.seedText;
  if(err) err.textContent='';
});
document.getElementById('seedClose').addEventListener('click',seedClose);
document.getElementById('seedSheet').addEventListener('mousedown',e=>{
  if(e.target.id==='seedSheet') seedClose();
});
/* Typed keys must reach the field and must NOT reach the game. WASD is movement, and on the title
   screen ANY key starts a run - so a field that leaked either would start a dungeon per letter. */
document.getElementById('seedInput').addEventListener('keydown',e=>{
  e.stopPropagation();
  if(e.key==='Enter'){ e.preventDefault(); seedDescend(); return; }
  if(e.key==='Escape'){ e.preventDefault(); seedClose(); return; }
});
// focus has to go somewhere sensible so the sheet is reachable by keyboard, and come back after
let uiReturnFocus=null;
function uiReleaseFocus(){
  if(uiReturnFocus&&uiReturnFocus.focus){ try{ uiReturnFocus.focus(); }catch(e){} }
  uiReturnFocus=null;
}
function showBugPanel(results){
  if(document.getElementById('bugBtn')){ toggleBugPanel(); return; }
  const CAT_ORDER=['frame and input','weapons and damage','the right click','enemies and AI',
    'movement and being hit','rooms and progression','HUD and interface','runs and records','presentation','unclassified'];
  const live=results&&results.length;
  const byName={};
  if(live) for(const r of results) byName[r.name]=r.ok;
  // With no results the table is the source of truth and everything in it is, by construction, fixed.
  const items=Object.keys(FIXES).map(name=>{
    const m=FIXES[name];
    return {name,cat:m[0],why:m[1],ok:live?byName[name]!==false:true,live:!!live,msg:live&&byName[name]===false?(results.find(r=>r.name===name)||{}).msg:''};
  });
  const byCat={};
  for(const c of CAT_ORDER) byCat[c]={ok:[],bad:[]};
  for(const it of items) byCat[it.cat][it.ok?'ok':'bad'].push(it);
  const pass=items.filter(i=>i.ok).length, total=items.length, allOk=pass===total;

  const btn=document.createElement('button');
  btn.id='bugBtn';
  btn.textContent=allOk?'bug fixes: '+pass+'/'+total+' ok':'bug fixes: '+(total-pass)+' FAILED';
  btn.title='Bug fixes (B)';
  btn.style.cssText='position:fixed;left:10px;bottom:10px;z-index:11;padding:7px 12px;cursor:pointer;'+
    'background:rgba(12,14,20,.92);color:'+(allOk?'#5ee27a':'#ff6b6b')+';border:1px solid '+(allOk?'#2f6b46':'#6b2f2f')+';'+
    'border-radius:4px;font:12px ui-monospace,monospace;letter-spacing:.04em';
  document.body.appendChild(btn);

  const panel=document.createElement('div');
  panel.id='bugPanel';
  panel.style.cssText='position:fixed;left:10px;bottom:44px;width:min(460px,calc(100vw - 20px));max-height:min(72vh,760px);'+
    'overflow:auto;display:none;z-index:11;padding:10px 12px;background:rgba(10,12,18,.95);'+
    'border:1px solid #2b303c;border-radius:4px;font:12px ui-monospace,monospace;box-shadow:0 8px 30px rgba(0,0,0,.5)';

  const head=document.createElement('div');
  head.style.cssText='display:flex;justify-content:space-between;align-items:baseline;margin-bottom:8px;color:'+(allOk?'#5ee27a':'#ff6b6b');
  const title=document.createElement('span');
  title.textContent=(allOk?'all fixed':'STILL BROKEN')+'  '+pass+'/'+total;
  const close=document.createElement('button');
  close.textContent='close';
  close.style.cssText='background:none;border:1px solid #3a4150;color:#9aa4b5;border-radius:3px;padding:2px 8px;cursor:pointer;font:inherit';
  close.onclick=()=>{panel.style.display='none'; releaseButtons(); keys={};};
  head.appendChild(title); head.appendChild(close);
  panel.appendChild(head);
  btn.onclick=()=>{ panel.style.display=panel.style.display==='none'?'block':'none';
    if(panel.style.display==='block') uiTakeInput(); };

  for(const c of CAT_ORDER){
    const g=byCat[c]; if(!g.ok.length&&!g.bad.length) continue;
    const det=document.createElement('details');
    // a group with a failure inside starts open, so red is never hidden behind a triangle
    det.open=!!g.bad.length||c==='unclassified';
    det.style.cssText='margin:0 0 3px;border-bottom:1px solid #1e222c';
    const sum=document.createElement('summary');
    const mark=g.bad.length?'✖':'✔';
    sum.style.cssText='cursor:pointer;padding:5px 2px;list-style:none;color:'+(g.bad.length?'#ff6b6b':'#8fd8a8')+';user-select:none';
    sum.innerHTML='<span style="opacity:.8">'+mark+'</span>  '+c+
      ' <span style="opacity:.5">'+g.ok.length+'/'+(g.ok.length+g.bad.length)+'</span>';
    det.appendChild(sum);
    const ul=document.createElement('ul');
    ul.style.cssText='margin:0 0 6px;padding:0 0 0 20px;list-style:none';
    // the reason comes first in grey, because "why is this pinned" is the part worth reading and
    // the name of the test is only there to find it in the source
    for(const it of g.ok.concat(g.bad)){
      const li=document.createElement('li');
      li.style.cssText='margin:0 0 5px;color:'+(it.ok?'#7fbf95':'#ff6b6b');
      const nm=document.createElement('div');
      nm.textContent=it.name;
      const why=document.createElement('div');
      why.textContent=it.why;
      why.style.cssText='color:'+(it.ok?'#5d6b78':'#c98a8a')+';font-size:11px;line-height:1.35';
      li.appendChild(nm); li.appendChild(why);
      if(!it.ok&&it.msg){
        const m=document.createElement('div');
        m.textContent=it.msg;
        m.style.cssText='color:#ff9a9a;font-size:11px;margin-top:2px';
        li.appendChild(m);
      }
      ul.appendChild(li);
    }
    det.appendChild(ul);
    panel.appendChild(det);
  }
  if(!live){
    const foot=document.createElement('div');
    foot.textContent='open the build with ?test to check these still hold';
    foot.style.cssText='color:#5d6b78;font-size:11px;margin-top:8px';
    panel.appendChild(foot);
  }
  document.body.appendChild(panel);
}
function toggleBugPanel(){
  const p=document.getElementById('bugPanel');
  if(!p) return;
  p.style.display=p.style.display==='none'?'block':'none';
  // same contract as the controls sheet: opening it takes the controls away, and a closed panel
  // must not leave the player walking or the wand firing because they were busy reading
  if(p.style.display==='block') uiTakeInput();
}

window.addEventListener('keydown',e=>{
  if(uiHoldsInput()&&!uiAllows(e.key)) return;
  const k=e.key.toLowerCase(), first=!e.repeat&&!keys[k];
  if(['arrowup','arrowdown','arrowleft','arrowright','w','a','s','d',' ','shift'].includes(k)) e.preventDefault();
  keys[k]=true;
  if(k==='f'&&first) showPerf=!showPerf;
  if(k==='g'&&first) showSpawn=!showSpawn;
  // the bug list is a key, not a URL. it lives in the main script and opens in a normal game, so
  // anyone who hits something odd can read the change history without a query string
  if(k==='b'&&first){ if(document.getElementById('bugBtn')) toggleBugPanel(); else showBugPanel(); return; }
  if(k==='h'&&first){ toggleControls(); return; }
  // S opens the seed sheet, but only where it means something: on the title screen and from a
  // pause. During play it stays strafe-down, because taking a movement key to open a dialog is how
  // a player dies while reading your menu.
  if(k==='s'&&first&&(state==='start'||paused)){ toggleSeedSheet(); return; }
  if(k==='escape'&&uiHoldsInput()){ uiCloseTop(); return; }
  if(state==='start'){startGame();return;}
  if(!first) return;
  if((k==='escape'||k==='p')&&state==='playing'){setPaused(!paused);return;}
  if(k==='r'&&(paused||state==='gameover'||state==='win')){startGame();return;}
  if((k==='shift'||k===' ')&&state==='playing'&&!paused&&!trans&&readyT<=0&&player.blinkCharges>0) doBlink();
});
window.addEventListener('keyup',e=>keys[e.key.toLowerCase()]=false);
window.addEventListener('blur',autoPause);
document.addEventListener('visibilitychange',()=>{if(document.hidden) autoPause();});
// clicking the dim area around the card closes the sheet, and a click ON the card does not. Both are
// decided from the event target rather than by stopping propagation, so the capture-phase suppressor
// above and this one cannot disagree about who saw the click.
document.getElementById('ctlSheet').addEventListener('mousedown',e=>{
  if(e.target.id==='ctlSheet') toggleControls();
});
canvas.addEventListener('mousemove',e=>{
  if(uiHoldsInput()) return;
  // measure from the content box: getBoundingClientRect includes the 2px CSS border
  const rect=canvas.getBoundingClientRect();
  mouse.x=(e.clientX-rect.left-canvas.clientLeft)*(canvas.width/canvas.clientWidth);
  mouse.y=(e.clientY-rect.top-canvas.clientTop)*(canvas.height/canvas.clientHeight);
});
// on the whole window, not just the canvas: the canvas is letterboxed, and a context menu opened
// over the dead space around it eats the mouseup that should have released the wand
window.addEventListener('contextmenu',e=>{e.preventDefault(); releaseButtons();});
window.addEventListener('mousedown',e=>{
  if(uiHoldsInput()) return;
  if(state==='start'){startGame();return;}
  if(paused){setPaused(false);return;}   // the resume click does not cast
  if(e.button===0||e.button===2) trackButton(e.button,true);
});
window.addEventListener('mouseup',e=>{
  if(!e.buttons){releaseButtons();return;}   // both buttons left in the same instant: one event clears both
  trackButton(e.button,false);
});
// e.buttons is 0 on any pointer move made with nothing held, so a latched button cannot survive
// the player moving the mouse again
window.addEventListener('mousemove',e=>{if(!e.buttons) releaseButtons();});
window.addEventListener('pointercancel',releaseButtons);
window.addEventListener('dragstart',e=>e.preventDefault());

requestAnimationFrame(loop);
