"""Additional tests after native_http.py, same isolated PostgreSQL database, new server process."""
import json
from pathlib import Path
import native_http as h

results=[]
try:
    h.start_server()
    id=h.ok('/board')[0]['service']['id']
    cases=[
        ('uppercase_checkout_read', '/CHECKOUT/SERVICES/'+id, None, 'service', 403),
        ('mixedcase_checkout_read', '/CheckOut/Services/'+id, None, 'kitchen', 403),
        ('uppercase_checkout_write', '/CHECKOUT/SERVICES/'+id+'/COMMANDS/payment', {}, 'service', 403),
        ('uppercase_release', '/OCCUPANCY/'+id+'/RELEASE', {}, 'service', 403),
        ('complete_only_main', '/SERVICES/'+id+'/COMMANDS/complete', {}, 'service', 403),
    ]
    for name,path,data,role,expected in cases:
        status,body=h.request(path,data,role=role)
        assert status==expected,(name,status,body)
        results.append({'name':name,'passed':True})
    status,body=h.request('/services',raw=b'null')
    assert status==422,('null_command',status,body)
    results.append({'name':'null_command','passed':True})
    print('HTTP authorization/validation: 6/6 passed')
finally:
    h.stop_server()
    Path('d1-security-results.json').write_text(json.dumps({'tests':len(results),'results':results},indent=2))
