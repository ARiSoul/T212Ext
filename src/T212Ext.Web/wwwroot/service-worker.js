// Makes T212Ext installable ("Install app" / "Add to Home Screen") and shows its notifications.
// Nothing is cached: a Blazor Server app needs the server anyway, so every page loads from the
// network, a deploy is picked up on the next visit, and no figure is ever shown stale offline.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));

// A notification from the server (Core/Notifications: title, body, url, tag).
self.addEventListener('push', event => {
    let data = {};
    try {
        data = event.data ? event.data.json() : {};
    } catch (e) {
        data = { body: event.data ? event.data.text() : '' };
    }
    event.waitUntil(self.registration.showNotification(data.title || 'T212Ext', {
        body: data.body || '',
        icon: '/icons/icon-192.png',
        badge: '/icons/badge-96.png',
        tag: data.tag || undefined,
        // One with the same tag is replaced, and the phone still rings.
        renotify: !!data.tag,
        data: { url: data.url || '/' }
    }));
});

// A tap opens its page: in a window of the app already open if there is one, else a new one.
self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = new URL((event.notification.data && event.notification.data.url) || '/', self.location.origin).href;
    event.waitUntil((async () => {
        const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        const open = windows.find(w => w.url.startsWith(self.location.origin));
        if (open) {
            await open.focus();
            try {
                await open.navigate(url);
                return;
            } catch (e) {
                // Not controlled by this worker yet: open a new window instead.
            }
        }
        await self.clients.openWindow(url);
    })());
});
