import json,sys,collections
d=json.load(open(sys.argv[1],encoding='utf-8'))
iss=d['issues'];print('issues',len(iss),'ms',d['metadata'].get('durationMs'))
for t in d['toolResults']: print(' ',t['toolId'],t['status'],t.get('issueCount'),t.get('filesAnalyzed'),str(t.get('errors'))[:200])
c=collections.Counter((i['toolId'],i['patternId'],i['severity'],i['category']) for i in iss)
for k,v in c.most_common(40): print(v,k)
print(collections.Counter(i['severity'] for i in iss))
f=collections.Counter(i['filePath'] for i in iss)
for k,v in f.most_common(15): print(v,k)
