"""Cheap deterministic proposals fitted to actual replays; predictions are NOT simulations."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['OMP_NUM_THREADS']='1'
import argparse
import json
import time
from pathlib import Path
import numpy as np
import balance as b

KEYS=['capital','cycles','refund','power','starterPower','generatorCapital','feedSpeed','researchCapital']
BOUNDS=np.array([[.5,12],[.35,2.5],[0,.75],[.5,2],[1,1.8],[.7,2.5],[.8,2],[.6,1.6]])


def features(x):
    return np.column_stack([np.ones(len(x)),x]+[x[:,i]*x[:,j] for i in range(x.shape[1]) for j in range(i,x.shape[1])])


def sequence(start,count):
    result=[]
    for prime in [2,3,5,7,11,13,17,19]:
        n=np.arange(start,start+count,dtype=np.int64);r=np.zeros(count);f=1.
        while np.any(n):
            f/=prime;r+=(n%prime)*f;n//=prime
        result.append(r)
    return np.column_stack(result)*(BOUNDS[:,1]-BOUNDS[:,0])+BOUNDS[:,0]


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run',type=Path,required=True);parser.add_argument('--count',type=int,default=1000000)
    parser.add_argument('--output',type=Path,required=True);a=parser.parse_args()
    manifest=b.read(a.run/'manifest.json');index=b.read(a.run/'screen.index.json')
    x=[];y=[]
    for i,p in enumerate(manifest['parameters']):
        r=[b.read(a.run/index[f'{i}:{n}']) for n in ['reference','minimum','over_6_reclaim']]
        if all(v['success'] for v in r):
            x.append([p[k] for k in KEYS]);y.append([v['seconds']/60 for v in r])
    x=np.array(x);y=np.log(np.array(y));mean=x.mean(axis=0);std=np.maximum(x.std(axis=0),.01)
    design=features((x-mean)/std)
    def fit(mask):
        f=design[mask];penalty=np.eye(f.shape[1])*2;penalty[0,0]=0
        return np.linalg.solve(f.T@f+penalty,f.T@y[mask])
    # Fixed five-fold held-out errors are reported before trusting any proposal.
    errors=[]
    for fold in range(5):
        test=np.arange(len(x))%5==fold
        errors.extend((design[test]@fit(~test)-y[test]).tolist())
    cv=np.sqrt(np.mean(np.array(errors)**2,axis=0))
    coefficients=fit(np.ones(len(x),dtype=bool));pool=[];start=time.monotonic()
    for offset in range(0,a.count,10000):
        values=sequence(offset+1,min(10000,a.count-offset));z=(values-mean)/std
        predicted=np.exp(np.clip(features(z)@coefficients,0,12));ref,minimum,extra=predicted.T
        ratio=minimum/ref;penalty=extra/ref-1
        loss=np.maximum(0,90-ref)/90+np.maximum(0,ref-120)/120+np.maximum(0,2.8-ratio)/2.8+np.maximum(0,ratio-3.2)/3.2+np.maximum(0,.3-penalty)/.3
        # Penalize distance to measured points; this does not make predictions trustworthy.
        distance=np.min(np.sum((z[:,None,:]-(x-mean)[None,:,:]/std)**2,axis=2),axis=1)
        score=loss+.03*distance
        for j in np.argsort(score,kind='stable')[:32]:
            pool.append(dict(score=float(score[j]),predictedLoss=float(loss[j]),parameters={k:round(float(v),5) for k,v in zip(KEYS,values[j])},predictedMinutes=predicted[j].tolist()))
        if (offset+10000)%100000==0:print(f'PREDICTED {min(offset+10000,a.count)}/{a.count}; no new game simulations',flush=True)
    selected=[]
    for r in sorted(pool,key=lambda v:(v['score'],b.canonical(v['parameters']))):
        z=(np.array([r['parameters'][k] for k in KEYS])-mean)/std
        if any(np.linalg.norm(z-(np.array([s['parameters'][k] for k in KEYS])-mean)/std)<.6 for s in selected):continue
        selected.append(r)
        if len(selected)==16:break
    report=dict(predictions=a.count,trainingSuccessfulEconomies=len(x),heldOutLogRMSE=cv.tolist(),
                warning='Approximate proposals only. All require actual game simulation; no balance pass is implied.',
                wallSeconds=time.monotonic()-start,proposals=selected)
    a.output.mkdir(parents=True,exist_ok=True);b.write(a.output/'predictions.json',report)
    b.write(a.output/'parameters.json',[r['parameters'] for r in selected])
    print(json.dumps({k:v for k,v in report.items() if k!='proposals'}))


if __name__=='__main__':main()
