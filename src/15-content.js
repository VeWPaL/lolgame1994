/* 15-content - the content registry Everything the game can be built out of is addressed by a stable string id and lives in one place, so that... [h:15-content-1] */

/* The hook table. A hook is a named piece of behaviour an ITEM can invoke without carrying any code
   with it. This is the seam that makes the item system versatile: the set of effects grows in code,
   and every data-only item keeps working. A mod can name anything in here. */
const HOOKS={};

/* THE FOUR THEMED BLOCKS OF THE CLIMB, as content. [h:15-content-2] */
const AREAS={
  Area1:{name:'THE SHALLOWS',
    flavour:'Cold stone, standing water, and seams cut badly by hand.'},
  Area2:{name:'THE KILN WORKS',
    flavour:'Warm brick, and a draught from something still burning.'},
  Area3:{name:'THE FLOODED GALLERIES',
    flavour:'Green light under the water, and a room being watched.'},
  Final:{name:"THE WARDEN'S DEEP",
    flavour:'Iron and red stone, and a door built from the inside.'},
};

const Content=(function(){
  /* A definition is copied on the way IN, not shared. A mod that hands us an object and keeps a
     reference can change the game's behaviour by mutating a dictionary it thinks it owns, and the
     resulting bug surfaces somewhere else entirely. */
  function clone(v){
    if(v===null||typeof v!=='object') return v;
    if(Array.isArray(v)) return v.map(clone);
    if(typeof v==='function') return v;              // hooks stay by reference, deliberately
    const o={};
    for(const k in v) o[k]=clone(v[k]);
    return o;
  }

  /* Weapons are identified by a display NAME and stored in an array, so their ids are derived
     rather than stored. "Arcane Beam" becomes "arcane_beam": stable, lowercase, no punctuation,
     and readable enough that a mod author can guess it. */
  function idOf(def){
    return String(def.name||'').toLowerCase().replace(/[^a-z0-9]+/g,'_').replace(/^_|_$/g,'');
  }

  const kinds={};
  function kind(name,table,required){
    const isArray=Array.isArray(table);
    kinds[name]={table:table,required:required,origin:{},array:isArray,pristine:null};
    /* A snapshot of the shipped content, taken once, at load. [h:15-content-3] */
    kinds[name].pristine=isArray?table.map(clone):clone(table);
    if(isArray){ for(const d of table) kinds[name].origin[idOf(d)]='game'; }
    else { for(const id in table) kinds[name].origin[id]='game'; }
  }

  kind('enemy',ENEMY,['r','hp','mass']);
  kind('weapon',WEAPONS,['name','dmg']);
  kind('item',{},['name']);
  /* AREAS, and the reason they are a kind rather than two constants near the palette. [h:15-content-4] */
  kind('area',AREAS,['name','flavour']);

  function all(name){
    const k=kinds[name];
    if(k.array) return k.table.map(idOf);
    return Object.keys(k.table);
  }
  function ids(name){ return all(name); }

  /* A getter, because a weapon lives at an index and the rest of the game wants a definition. */
  function get(name,id){
    const k=kinds[name];
    if(!k) throw new Error('Content: unknown kind "'+name+'"');
    let d;
    if(k.array) d=k.table.find(x=>idOf(x)===id);
    else d=k.table[id];
    /* LOUD on purpose. A missing id used to be `undefined` flowing silently into a stat read and
       becoming a NaN three frames later, somewhere else. It throws here, with the id in the
       message, where the mistake actually is. */
    if(!d) throw new Error('Content: no '+name+' called "'+id+'" (have: '+all(name).join(', ')+')');
    return d;
  }
  function has(name,id){
    const k=kinds[name];
    if(!k) return false;
    return k.array? k.table.some(x=>idOf(x)===id) : !!k.table[id];
  }

  /* One iteration shape for both storage shapes. [h:15-content-5] */
  function each(name,fn){
    const k=kinds[name];
    if(k.array) k.table.forEach(d=>fn(idOf(d),d));
    else for(const id in k.table) fn(id,k.table[id]);
  }

  /* Returns the list of problems rather than throwing. A mod folder with one broken file should
     load the other nine and report the tenth, not take the game down with it. */
  function validate(){
    const out=[];
    for(const name in kinds){
      const required=kinds[name].required, seen={};
      each(name,(id,d)=>{
        if(kinds[name].origin[id]!=='mod') kinds[name].origin[id]='game';
        if(seen[id]) out.push(name+' "'+id+'" is defined twice');
        seen[id]=1;
        for(const f of required){
          if(d[f]===undefined) out.push(name+' "'+id+'" is missing '+f);
        }
        if(d.hooks) for(const h in d.hooks){
          if(typeof HOOKS[h]!=='function') out.push(name+' "'+id+'" names hook "'+h+'", which does not exist');
        }
        if(d.stats) for(const s in d.stats){
          if(typeof d.stats[s]!=='number') out.push(name+' "'+id+'" has a non-numeric stat "'+s+'"');
        }
      });
    }
    return out;
  }

  function define(name,id,def,origin){
    const k=kinds[name];
    if(!k) return ['unknown kind "'+name+'"'];
    if(!id||typeof id!=='string') return ['id must be a non-empty string, got '+JSON.stringify(id)];
    if(!def||typeof def!=='object') return [name+' "'+id+'" has no definition'];
    const fromMod=origin==='mod';
    if(has(name,id)&&!fromMod) return [name+' "'+id+'" already exists'];
    if(fromMod&&has(name,id)){
      // an override replaces in place, so a mod that patches a gun does not silently reorder the
      // array and change every index after it
      if(k.array){
        const i=k.table.findIndex(x=>idOf(x)===id);
        k.table[i]=clone(def);
      } else k.table[id]=clone(def);
    } else {
      if(k.array){ def.name=def.name||id; k.table.push(clone(def)); }
      else k.table[id]=clone(def);
    }
    k.origin[id]=fromMod?'mod':'game';
    /* The pristine copy is the SHIPPED definition, and it is captured HERE rather than when the kind was declared. [h:15-content-6] */
    if(!fromMod) k.pristine[id]=clone(def);
    return [];
  }

  /* Mods. A mod is a folder of JSON files; each file holds definitions by kind. Failures are
     collected and never thrown: a friend with a broken mod gets a warning, not a game that will
     not start. That isolation is the whole reason this goes through a list rather than a try. */
  const mods={loaded:[],errors:[],sources:[]};

  function loadMods(list){
    mods.loaded=[]; mods.errors=[]; mods.sources=[];
    for(const m of list||[]){
      const name=m.name||'(unnamed)';
      mods.sources.push(name);
      for(const file of (m.defs||[])){
        for(const kindName in file){
          for(const id in file[kindName]){
            const errs=define(kindName,id,file[kindName][id],'mod');
            if(errs.length){ mods.errors.push(name+': '+errs.join('; ')); continue; }
            mods.loaded.push(kindName+'/'+id);
          }
        }
      }
    }
    for(const b of validate()) mods.errors.push('validation: '+b);
    return mods;
  }

  /* Drops every mod override and returns the game's own content. Used by the tests, and by anyone
     who wants to see what they are actually being modded on top of. */
  function resetMods(){
    for(const name in kinds){
      const k=kinds[name];
      // rebuild from the snapshot rather than trying to unpick. Order matters - the game indexes
      // weapons - and a splice-based undo cannot put an overridden weapon back in its original
      // POSITION, only delete the thing that replaced it.
      if(k.array){
        k.table.length=0;
        for(const d of k.pristine) k.table.push(clone(d));
      } else {
        for(const id in k.table) delete k.table[id];
        for(const id in k.pristine) k.table[id]=clone(k.pristine[id]);
      }
      for(const id in k.origin) if(k.origin[id]==='mod') delete k.origin[id];
    }
    mods.loaded=[]; mods.errors=[]; mods.sources=[];
  }

  return {define:define,get:get,has:has,all:all,ids:ids,validate:validate,
          loadMods:loadMods,resetMods:resetMods,mods:mods,kind:kind,idOf:idOf};
})();
