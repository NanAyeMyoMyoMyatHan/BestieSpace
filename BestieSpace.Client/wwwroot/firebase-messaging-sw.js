/* ==========================================================================
   BestieSpace Firebase Cloud Messaging Service Worker (Background Web Push)
   ========================================================================== */

importScripts('https://www.gstatic.com/firebasejs/10.8.0/firebase-app-compat.js');
importScripts('https://www.gstatic.com/firebasejs/10.8.0/firebase-messaging-compat.js');

self.addEventListener('install', (event) => {
    console.log('[BestieSpace FCM SW] Service Worker installed.');
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    console.log('[BestieSpace FCM SW] Service Worker activated.');
    event.waitUntil(self.clients.claim());
});

let fcmMessaging = null;

// Initialize Firebase inside the Service Worker when config message arrives
self.addEventListener('message', (event) => {
    if (event.data && event.data.type === 'INIT_FIREBASE_SW' && event.data.config) {
        try {
            if (!firebase.apps.length) {
                firebase.initializeApp(event.data.config);
                fcmMessaging = firebase.messaging();

                fcmMessaging.onBackgroundMessage((payload) => {
                    console.log('[BestieSpace FCM SW] Received background push: ', payload);

                    const title = payload.notification?.title || payload.data?.title || 'BestieSpace 🌸';
                    const options = {
                        body: payload.notification?.body || payload.data?.body || 'You have a new bestie update! 💕',
                        icon: payload.notification?.icon || '/favicon.png',
                        badge: '/favicon.png',
                        tag: payload.data?.type || 'bestiespace-alert',
                        data: {
                            clickUrl: payload.fcmOptions?.link || payload.data?.clickUrl || '/'
                        }
                    };

                    self.registration.showNotification(title, options);
                });
                console.log('[BestieSpace FCM SW] Firebase messaging listener configured in SW.');
            }
        } catch (e) {
            console.warn('[BestieSpace FCM SW] Error configuring Firebase in SW:', e);
        }
    }
});

// Handle notification click action (open tab or focus window)
self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const clickUrl = event.notification.data?.clickUrl || '/';

    event.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windowClients) => {
            for (let i = 0; i < windowClients.length; i++) {
                const client = windowClients[i];
                if (client.url.includes(self.location.origin) && 'focus' in client) {
                    if (clickUrl && clickUrl !== '/' && !client.url.endsWith(clickUrl)) {
                        client.navigate(clickUrl);
                    }
                    return client.focus();
                }
            }
            if (self.clients.openWindow) {
                return self.clients.openWindow(clickUrl);
            }
        })
    );
});
