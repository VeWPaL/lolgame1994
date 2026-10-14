/* constant-audit.js - compares the C# port's constants against the running game, BY VALUE.
 *
 * Why this exists: two stale constants have already been found by hand in Balance.cs, and both were
 * found only because something happened to read them.
 *
 *   SwerveDecay   0.011 against the game's 0.0035 - the meter forgot a reversal three times faster
 *   BrunchRamp    Sec(2.2) against the game's 1.5s   - the PRE-TUNING value, kept through a
 *                 deliberate change, so the port still had the ramp the game had replaced
 *
 * Both sat in the file whose entire job is to hold the game's numbers, and the C# suite passed 185/185
 * with either of them wrong. An unpinned constant there is indistinguishable from a correct one.
 *
 * This reads the values out of the running game and out of the C# source and compares them. It is
 * REPORT-ONLY: it never edits either side, because an audit that fixes what it finds cannot tell you
 * how much was wrong.
 *
 *   node tools/constant-audit.js        (needs the local server on 127.0.0.1:8791)
 *
 * A NOTE ON THE PARSER, which is the fourth false-positive of this shape in the port. A declaration
 * can name several constants at once - `public const int A = 1, B = 2;` - and reading only the first
 * made four correct constants look absent. That is the false-positive twin of the bug this audit
 * exists to find, and it is worth stating because "the audit says four are missing" and "the audit
 * says four are wrong" are very different findings wearing the same output.
 */

const path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA||'', 'hermes/hermes-agent/node_modules/playwright'));
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:800,height:600}});
  await p.goto('http://127.0.0.1:8791/depths.html',{waitUntil:'domcontentloaded',timeout:30000});
  await p.waitForTimeout(1500);
  const js=await p.evaluate(()=>{
    const g=(n)=>{ try{ return eval(n); }catch(e){ return null; } };
    const out={};
    for(const n of ['BRUNCH_SCAN_TICKS','BRUNCH_SHIELD_MIN','BRUNCH_WALL_MIN','BRUNCH_SHIELD_SPEED',
      'BRUNCH_ACCEL','BRUNCH_DECEL','BRUNCH_DEADZONE','BRUNCH_RAMP','BRUNCH_RAMP_GAIN','BRUNCH_RUN',
      'BRUNCH_CHASE_SPEED','BRUNCH_ARC_GAP','GUARD_STANDOFF_MULT','PRESSURE_CLOSURE','AGGRO_TIME',
      'HIT_SLOW_MULT','SWERVE_AIM','SWERVE_TRUST_ROOTED','SWERVE_TRUST_WALKING','CAST_TIME',
      'KNOCK_CUT','KNOCK_GAIN','KNOCK_P_GAIN','KNOCK_FRICTION','SWERVE_GAIN','SWERVE_DECAY',
      'HIT_FLASH','MOVE_ACCEL','MOMENTUM_ACCEL','LUNGE_TRACK','LUNGE_HOLD','LUNGE_MIN','LUNGE_REACH',
      'LUNGE_FLOOR','LUNGE_ITER','LUNGE_SPEED','LUNGE_RANGE','PLAYER_HIT_R','PLAYER_HIT_DY',
      'BLINK_RECHARGE','TICK_HZ'])
      out[n]=g(n);
    out['GUNNER_DODGE.sight']=g('GUNNER_DODGE.sight');
    out['GUNNER_DODGE.chance']=g('GUNNER_DODGE.chance');
    out['GUNNER_DODGE.kick']=g('GUNNER_DODGE.kick');
    out['GUNNER_DODGE.cd']=g('GUNNER_DODGE.cd');
    out['ENEMY.brunch.r']=g('ENEMY.brunch.r');
    out['ENEMY.brunch.walk']=g('ENEMY.brunch.walk');
    out['player.speed']=g('player.speed');
    out['player.r']=g('player.r');
    return out;
  });
  await b.close();
  // the C# values, from the C# itself (Depths.Playtest `constants`: every Balance dial at the JS rate, untuned):
  // a regex over the source silently lost every dial written in per-second units
  const root=path.join(__dirname,'..');
  const out=require('child_process').execFileSync('dotnet',['run','--project',path.join(root,'csharp','Depths.Playtest'),'-c','Release','--','constants'],{encoding:'utf8'});
  const csVals=JSON.parse(out.trim().split('\n').pop());
  // map the JS names to the C# names
  const MAP={
    BRUNCH_SCAN_TICKS:'BrunchScanTicks', BRUNCH_SHIELD_MIN:'BrunchShieldMin',
    BRUNCH_WALL_MIN:'BrunchWallMin', BRUNCH_SHIELD_SPEED:'BrunchShieldSpeed',
    BRUNCH_ACCEL:'BrunchAccel', BRUNCH_DECEL:'BrunchDecel', BRUNCH_DEADZONE:'BrunchDeadzone',
    BRUNCH_RAMP:'BrunchRamp', BRUNCH_RAMP_GAIN:'BrunchRampGain', BRUNCH_RUN:'BrunchRun',
    BRUNCH_CHASE_SPEED:'BrunchRun', BRUNCH_ARC_GAP:'BrunchArcGap',
    GUARD_STANDOFF_MULT:'GuardStandoffMult', PRESSURE_CLOSURE:'PressureClosure',
    AGGRO_TIME:'AggroTime', HIT_SLOW_MULT:'HitSlowMult', SWERVE_AIM:'SwerveAim',
    SWERVE_TRUST_ROOTED:'SwerveTrustRooted', SWERVE_TRUST_WALKING:'SwerveTrustWalking',
    CAST_TIME:'CastTime', KNOCK_CUT:'KnockCut', KNOCK_GAIN:'KnockGain', KNOCK_P_GAIN:'KnockPGain',
    KNOCK_FRICTION:'KnockFriction', SWERVE_GAIN:'SwerveGain', SWERVE_DECAY:'SwerveDecay',
    HIT_FLASH:'HitFlash', MOVE_ACCEL:'MoveAccel', MOMENTUM_ACCEL:'MomentumAccel',
    LUNGE_TRACK:'LungeTrack', LUNGE_HOLD:'LungeHold', LUNGE_MIN:'LungeMin', LUNGE_REACH:'LungeReach',
    LUNGE_FLOOR:'LungeFloor', LUNGE_ITER:'LungeIter', LUNGE_SPEED:'LungeSpeed',
    LUNGE_RANGE:'LungeRange', PLAYER_HIT_R:'PlayerHitR', PLAYER_HIT_DY:'PlayerHitDy',
    BLINK_RECHARGE:'BlinkRecharge', TICK_HZ:'TickHz',
    'GUNNER_DODGE.sight':'GunnerDodgeSight', 'GUNNER_DODGE.chance':'GunnerDodgeChance', 'GUNNER_DODGE.kick':'GunnerDodgeKick',
    'GUNNER_DODGE.cd':'GunnerDodgeCd', 'ENEMY.brunch.r':null, 'ENEMY.brunch.walk':null,
    'player.speed':'speed', 'player.r':'r',
  };
  const rows=[]; let bad=0, missing=0;
  for(const [jsName, csName] of Object.entries(MAP)){
    const jv=js[jsName];
    if(jv===null||typeof jv!=='number') continue;
    if(csName===null){ rows.push([jsName,jv,'(not ported)','-',false]); continue; }
    if(!(csName in csVals)){ missing++; rows.push([jsName,jv,'MISSING in C#',csName,false]); continue; }
    const cv=csVals[csName];
    const ok=Math.abs(cv-jv)<1e-9;
    if(!ok) bad++;
    rows.push([jsName,jv,csName,cv,ok]);
  }
  const w=[22,10,24,10,4];
  console.log(['JS constant','game','C# name','C# value',''].map((s,i)=>s.padEnd(w[i])).join(''));
  for(const r of rows) console.log(String(r[0]).padEnd(w[0])+String(r[1]).padEnd(w[1])+String(r[2]).padEnd(w[2])+String(r[3]).padEnd(w[3])+(r[4]?'ok':'  <<< MISMATCH'));
  console.log(`\n${rows.length} compared, ${bad} mismatch, ${missing} missing`);
})();
