import test from 'node:test';
import assert from 'node:assert/strict';
import { createSocket } from 'node:dgram';
import { startDiscovery, DISCOVERY_PORT } from '../src/host/discovery.js';

test('discovery: IPv4 UDP listener receives LOBOVER protocol packet and its 2-second repeat', { timeout: 6000 }, async () => {
  const listener=createSocket({type:'udp4',reuseAddr:true});let beacon;
  await new Promise(resolve=>listener.bind(DISCOVERY_PORT,'0.0.0.0',resolve));
  // A real game may also be broadcasting on the fixed discovery port during tests.
  const nextPacket=()=>new Promise(resolve=>{
    const receive=packet=>{if(packet.toString().endsWith(' test-pc')){listener.off('message',receive);resolve(packet);}};
    listener.on('message',receive);
  });
  try {
    const first=nextPacket();
    beacon=await startDiscovery(7331,{interfaces:[{address:'127.0.0.1',netmask:'255.0.0.0'}],name:'test-pc'});
    const packet=await first;
    assert.equal(packet.toString(),'LOBOVER 1 7331 test-pc');
    const began=performance.now();
    const repeat=await nextPacket();
    assert.equal(repeat.toString(),packet.toString());
    assert.ok(performance.now()-began>=1800);
    console.log('UDP 7332: LOBOVER 1 7331 test-pc (initial + 2 s repeat)');
  } finally {beacon?.close();listener.close();}
});
