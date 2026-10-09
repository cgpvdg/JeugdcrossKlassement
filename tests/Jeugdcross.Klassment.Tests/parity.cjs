// Evaluate the actual existing Vue calculations; no duplicate reference implementation.
const fs = require('node:fs');
const vue = require('../../JeugdcrossKlassement/node_modules/vue');
const source = fs.readFileSync('JeugdcrossKlassement/src/App.vue','utf8').split('<script setup>')[1].split('</script>')[0].replace(/^import[\s\S]*?from ['"][^'"]+['"]\s*$/gm,'');
const categories = [...fs.readFileSync('JeugdcrossKlassement/src/categories.js','utf8').matchAll(/'([^']+ U\d+)'/g)].map(m=>m[1]);
const key = value => value.trim().toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/[^a-z0-9]+/g,' ').trim();
const cases = [];
for (let seed=0;seed<10;seed++) {
 const input = { Version:1, Races:[], Decisions:[], NameOverrides:{} };
 for(let race=0;race<3;race++) {
  const r={ Id:`r${race}`, Name:`Cross ${race}`, Date:`2026-10-${10+race}T00:00:00`, Poule:'Noord', Results:[] };
  for(const category of ['Jongens U16','Mannen U18','Mannen U20'])
  for(let runner=0;runner<6+seed;runner++) {
   if((runner+race+seed)%7===0)continue;
   const points=1+(runner*3+race*2+seed)%15;
   r.Results.push({Category:category,Name:`Deelnemer ${runner}`,Association:runner%4===0?'NTB':`AV ${runner%3}`,Rank:points,Points:points,Time:'4:02'});
  }
  input.Races.push(r);
 }
 const api=new Function('computed','markRaw','onMounted','ref','shallowRef','CATEGORY_ORDER','normalizeParticipantKey','decodeTextFileFromArrayBuffer','parseCrossMetadata','parseCrossResults',source+'\nreturn {allCrosses,allResults,standingsPerCategory,teamStandingsPerCategory};')(vue.computed,vue.markRaw,()=>{},vue.ref,vue.shallowRef,categories,key,()=>{},()=>{},()=>{});
 api.allCrosses.value=input.Races.map(r=>({id:r.Id,poule:r.Poule,name:r.Name,date:r.Date}));
 api.allResults.value=input.Races.flatMap(r=>r.Results.map(x=>({crossId:r.Id,category:x.Category,participantName:x.Name,participantKey:key(x.Name),association:x.Association,points:x.Points,time:x.Time})));
 cases.push({label:`seed ${seed}`,input,individual:[...api.standingsPerCategory.value.values()].flat().map(x=>({name:x.displayName,total:x.total,place:x.place,qualified:x.isQualifiedForFinal})),teams:[...api.teamStandingsPerCategory.value.values()].flat().map(x=>({association:x.association,total:x.total,place:x.place,qualified:x.isQualifiedForFinal}))});
}
fs.mkdirSync('Jeugdcross.Klassment/artifacts',{recursive:true});fs.writeFileSync('Jeugdcross.Klassment/artifacts/parity.json',JSON.stringify(cases));
console.log(`Generated ${cases.length} reference cases from original App.vue.`);
