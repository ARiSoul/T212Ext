// Makes T212Ext installable ("Install app" / "Add to Home Screen").
// Nothing is cached: a Blazor Server app needs the server anyway, so every page loads from the
// network, a deploy is picked up on the next visit, and no figure is ever shown stale offline.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
