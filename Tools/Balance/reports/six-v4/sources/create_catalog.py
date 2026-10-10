"""Generate editable Unity JSON balance data from the supplied v2 baseline."""
import json
import argparse
from pathlib import Path

items = ['Iron Ore','Copper Ore','Carbon','Silicate','Crude Steel','Copper','Steel Plate','Steel Beam','Steel Rod','Copper Wire','Copper Bus','Refractory Block','Structural Frame S','Bearing Unit S','Basic Electrical Part','Motor Core S','Motor S','Actuator S','Pump S','Power Unit S','Control Unit S','Electrical Cabinet','Drive Assembly','Cooling Assembly','Furnace Module','Fuel Cell','Propellant','Basic Ammo','Armor Panel S','Airframe','Thruster Module','Fuel System','Avionics Package','Weapon Mount','Fighter Mk1','Construction Drone S']
ids = ['iron','copperOre','carbon','silicate','steel','copper','plate','beam','rod','wire','bus','refractory','frame','bearing','electrical','motorCore','motor','actuator','pump','power','control','cabinet','drive','cooling','furnace','fuel','propellant','ammo','armor','airframe','thruster','fuelSystem','avionics','mount','fighter','drone']
def stacks(s):
    return [{'item':p.split(':')[0],'amount':int(p.split(':')[1])} for p in s.split()]
recipes=[]
def recipe(id,name,family,inputs,output,seconds,tech=0):
    recipes.append(dict(id=id,name=name,family=family,inputs=stacks(inputs),outputs=stacks(output),seconds=seconds,tech=tech))
base=[('plate','Steel Plate','steel:1','plate:1',10,0),('beam','Steel Beam','steel:2','beam:1',20,0),('rod','Steel Rod','steel:1','rod:2',20,0),('wire','Copper Wire','copper:1','wire:2',10,0),('bus','Copper Bus','copper:2','bus:1',20,0),('refractory','Refractory Block','silicate:2','refractory:1',15,0),('frame','Structural Frame S','beam:2 plate:2','frame:1',40,1),('bearing','Bearing Unit S','rod:1 plate:1','bearing:1',30,1),('electrical','Basic Electrical Part','bus:1 wire:2','electrical:1',30,1),('motorCore','Motor Core S','plate:1 bearing:1 wire:2','motorCore:1',45,1),('motor','Motor S','motorCore:1 bearing:1 wire:2 plate:1','motor:1',60,2),('actuator','Actuator S','motorCore:1 bearing:1 rod:2','actuator:1',60,2),('pump','Pump S','motor:1 plate:1 rod:1','pump:1',75,2),('power','Power Unit S','bus:1 electrical:2 wire:2 plate:1','power:1',75,2),('control','Control Unit S','electrical:2 wire:2 plate:1','control:1',90,2)]
for a,n,i,o,s,t in base: recipe('work_'+a,n,'workshop',i,o,s,t)
for a,f,s in [('plate','structural',5),('beam','structural',20/3),('bus','electrical',7.5),('frame','structural',15),('bearing','mechanical',12),('electrical','electrical',30),('motorCore','mechanical',18),('motor','mechanical',24),('actuator','mechanical',26),('pump','mechanical',30),('power','electrical',30),('control','electrical',36)]:
    b=next(v for v in base if v[0]==a); recipe('spec_'+a,b[1],f,b[2],b[3],s,2)
for a,n,f,i,s in [('cabinet','Electrical Cabinet','electrical','power:2 control:2 plate:2',60),('drive','Drive Assembly','mechanical','motor:2 bearing:2 frame:2 plate:2',75),('cooling','Cooling Assembly','fluid','pump:2 plate:2 frame:1 refractory:1',90),('furnace','Furnace Module','structural','refractory:4 plate:2 frame:2',60)]: recipe(a,n,f,i,a+':1',s,2)
for family,sec in [('smelter',10),('industrialSmelter',10/3)]:
    recipe(family+'_steel','Smelt iron',family,'iron:1','steel:1',sec)
    recipe(family+'_copper','Smelt copper',family,'copperOre:1','copper:1',sec)
for a,n,f,i,o,s in [('fuel','Refined Fuel','fuel','carbon:2','fuel:1',20),('propellant','Propellant','fuel','carbon:2','propellant:1',20),('ammo','Basic Ammo','ammo','rod:1 propellant:1','ammo:10',15),('armor','Armor Panel S','structural','plate:2','armor:1',20),('airframe','Airframe','structural','frame:8 plate:10','airframe:1',120),('thruster','Thruster Module','mechanical','motor:2 pump:1 power:1 frame:1 plate:2','thruster:1',90),('fuelSystem','Fuel System','fluid','pump:2 control:1 frame:1 plate:2','fuelSystem:1',90),('avionics','Avionics Package','electrical','control:2 power:1 wire:4','avionics:1',90),('mount','Weapon Mount','mechanical','actuator:1 control:1 frame:1 plate:2','mount:1',60),('fighter','Fighter Mk1','fighter','airframe:1 thruster:2 fuelSystem:1 avionics:1 mount:2 armor:6','fighter:1',600)]: recipe(a,n,f,i,o,s,4)
recipe('drone','Construction Drone S','drone','frame:1 motor:1 actuator:1 control:1 power:1','drone:1',90,3)
buildings=[]
def building(id,name,family,cost,time,power,tech=0,size=2,rate=0,generation=0,fuelRate=0):
    buildings.append(dict(id=id,name=name,family=family,cost=stacks(cost),seconds=time,power=power,tech=tech,size=size,rate=rate,generation=generation,fuelRate=fuelRate))
building('core','Command Core','core','',0,0,size=3,generation=80)
building('miner','Basic Miner','miner','plate:12 wire:4',20,12,rate=6)
building('smelter','Primitive Smelter','smelter','plate:16 refractory:6 wire:4',30,18)
building('generator','Basic Generator','generator','plate:18 wire:8 refractory:4',35,0,generation=160,fuelRate=3)
building('workshop','Universal Workshop S','workshop','plate:14 wire:6 refractory:2',40,24)
building('storage','Storage S','storage','plate:8',15,0,size=1)
building('belt','T1.1 Belt','belt','plate:1',1,4,size=1,rate=3)
building('splitter','Six-port Splitter','splitter','plate:4 wire:2',8,8,size=2,rate=3)
building('pole','Power Node','pole','plate:1 wire:2',5,0,size=1)
building('structural','Structural Fabricator S','structural','plate:16 beam:8 motor:1 control:1',75,30,2)
building('mechanical','Mechanical Assembler S','mechanical','plate:8 frame:6 motor:2 control:1',75,32,2)
building('electrical','Electrical Assembler S','electrical','plate:8 frame:6 motor:1 electrical:2 control:1',75,30,2)
building('fluid','Fluid/Thermal Assembler S','fluid','plate:10 frame:6 pump:2 control:1',90,34,2)
building('industrialMiner','Industrial Miner','miner','plate:6 frame:4 motor:2 control:1',120,30,3,rate=18)
building('industrialSmelter','Industrial Smelter','industrialSmelter','plate:8 frame:4 drive:1 cooling:1 cabinet:1 refractory:6',180,55,3,size=3)
building('industrialGenerator','Industrial Generator','generator','plate:10 frame:6 motor:2 pump:2 power:1 control:1',180,0,3,size=3,generation=450,fuelRate=6)
building('droneBay','Construction Drone Bay','drone','plate:8 frame:6 motor:2 control:2',150,25,3,size=3)
building('fuelProcessor','Fuel Processor S','fuel','plate:8 frame:4 pump:1 control:1',90,35,4)
building('ammoPress','Ammo Press S','ammo','plate:8 frame:4 motor:1 control:1',90,30,4)
building('fighterPad','Fighter Assembly Pad','fighter','plate:16 frame:10 drive:2 cabinet:2',240,60,4,size=4)
techs=[]
for id,name,cost,sec,power in [(1,'Basic Industry I','plate:20 wire:10 refractory:4',60,20),(2,'Component Industry I','frame:4 motorCore:4 electrical:4 wire:20',120,25),(3,'Industrial Construction I','frame:8 motor:4 pump:2 power:2 control:2',180,30),(4,'Flight Preparation I','frame:12 motor:6 pump:4 power:4 control:4',240,40)]:
    techs.append(dict(id=id,name=name,cost=stacks(cost),seconds=sec,power=power))
# v2 authoritative changes; legacy item IDs remain valid for existing saves.
def change(id, inputs=None, seconds=None, name=None):
    r=next(r for r in recipes if r['id']==id)
    if inputs is not None:r['inputs']=stacks(inputs)
    if seconds is not None:r['seconds']=seconds
    if name is not None:r['name']=name
change('work_electrical',seconds=40)
change('work_control',inputs='electrical:1 wire:2 plate:1')
change('spec_control',inputs='electrical:1 wire:2 plate:1',seconds=30)
change('spec_beam',seconds=7.5)
recipe('spec_rod','Steel Rod','structural','steel:1','rod:2',10,2)
recipe('spec_wire','Copper Wire','electrical','copper:1','wire:2',5,2)
recipe('spec_refractory','Refractory Block','structural','silicate:2','refractory:1',7.5,2)
change('drive',inputs='motor:2 bearing:2 frame:1',seconds=45)
change('cooling',inputs='pump:2 frame:1 plate:2',seconds=60)
change('cabinet',inputs='power:2 control:2 frame:1 bus:2')
change('airframe',inputs='frame:24 beam:20 plate:30',seconds=300,name='Fighter Skeleton')
change('thruster',inputs='motor:4 pump:2 power:2 frame:2 plate:4',seconds=180,name='Thruster Module Mk1')
change('fuelSystem',inputs='pump:4 control:2 frame:2 plate:4',seconds=180,name='Fuel System Mk1')
change('avionics',inputs='control:4 power:2 electrical:4 bus:4',seconds=180,name='Avionics Package Mk1')
change('mount',inputs='actuator:2 motor:1 control:1 frame:2 plate:2',seconds=150,name='Weapon Mount Mk1')
recipes[:]=[r for r in recipes if r['id']!='fighter']
items.extend(['Fuel System S','Ammo Handling Module']);ids.extend(['bayFuel','ammoHandling'])
items[ids.index('airframe')]='Fighter Skeleton'
items[ids.index('fuelSystem')]='Fuel System Mk1'
recipe('bayFuel','Fuel System S','fluid','pump:2 control:1 frame:1 plate:2','bayFuel:1',90,4)
recipe('ammoHandling','Ammo Handling Module','mechanical','actuator:2 motor:1 control:1 frame:1','ammoHandling:1',90,4)
recipe('emergency_plate','Emergency Steel Plate','emergency','iron:1','plate:1',60)
recipe('emergency_wire','Emergency Copper Wire','emergency','copperOre:1','wire:2',60)
recipe('emergency_refractory','Emergency Refractory','emergency','silicate:2','refractory:1',60)
for r in recipes:
    if r['family']=='emergency':r['powerOverride']=200
bay=next(b for b in buildings if b['id']=='fighterPad')
bay.update(name='Fighter Bay Megaproject',family='fighter',cost=[],seconds=0,power=40)
techs[-1].update(name='Orbital Operations I',cost=stacks('frame:20 motor:8 pump:6 power:6 control:6 drive:4'),seconds=600,power=60)

data=dict(items=[dict(id=i,name=n,capacity=40,category='raw' if ix<4 else 'component',cargoClass='small') for ix,(i,n) in enumerate(zip(ids,items))],recipes=recipes,buildings=buildings,techs=techs)
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--baseline',action='store_true',help='Generate the pre-expansion v2 baseline')
parser.add_argument('--output',type=Path,help='Alternate output path')
args=parser.parse_args()
if not args.baseline:
    profile=json.loads((Path(__file__).resolve().parent/'Balance/profiles/six-v4.json').read_text())
    for group in ('buildings','recipes','techs'):
        by_id={str(entry['id']):entry for entry in data[group]}
        for id,changes in profile.get(group,{}).items():
            by_id[id].update(changes)
path=args.output or Path(__file__).resolve().parents[1]/'Assets/Resources/catalog.json'
path.parent.mkdir(parents=True,exist_ok=True)
path.write_text(json.dumps(data,indent=2))
print(f'{len(items)} items, {len(recipes)} recipes, {len(buildings)} buildings, {len(techs)} technologies -> {path}')
