/* ==========================================================================
   BestieSpace Firebase Cloud Messaging Client Interop (Realtime Push)
   ========================================================================== */

(function () {
    let fcmApp = null;
    let fcmMessaging = null;
    let dotNetRef = null;
    let swRegistration = null;

    window.bestieFcm = {
        isSupported: function () {
            return ('serviceWorker' in navigator) && ('Notification' in window) && ('PushManager' in window);
        },

        getPermissionState: function () {
            if (!('Notification' in window)) return 'unsupported';
            return Notification.permission; // 'default', 'granted', 'denied'
        },

        init: async function (config, dotNetHelper) {
            dotNetRef = dotNetHelper;

            if (!window.bestieFcm.isSupported()) {
                console.log('[BestieSpace FCM] Push notifications not supported in this browser.');
                return false;
            }

            try {
                // Register Service Worker
                swRegistration = await navigator.serviceWorker.register('/firebase-messaging-sw.js', {
                    scope: '/'
                });
                console.log('[BestieSpace FCM] Service worker registered successfully with scope:', swRegistration.scope);

                // Pass config to Service Worker for background notifications
                if (config && config.apiKey && config.projectId) {
                    if (navigator.serviceWorker.controller) {
                        navigator.serviceWorker.controller.postMessage({
                            type: 'INIT_FIREBASE_SW',
                            config: config
                        });
                    } else {
                        navigator.serviceWorker.ready.then((reg) => {
                            if (reg.active) {
                                reg.active.postMessage({
                                    type: 'INIT_FIREBASE_SW',
                                    config: config
                                });
                            }
                        });
                    }

                    // Initialize Firebase in window context
                    if (typeof firebase !== 'undefined') {
                        if (!firebase.apps.length) {
                            fcmApp = firebase.initializeApp(config);
                        } else {
                            fcmApp = firebase.app();
                        }

                        fcmMessaging = firebase.messaging();

                        // Listen for foreground push messages
                        fcmMessaging.onMessage((payload) => {
                            console.log('[BestieSpace FCM] Foreground push received:', payload);
                            const title = payload.notification?.title || payload.data?.title || 'BestieSpace 🌸';
                            const body = payload.notification?.body || payload.data?.body || 'New alert from your besties! 💕';
                            const clickUrl = payload.data?.clickUrl || '/';

                            // Show in-app banner/toast & local notification if permitted
                            window.bestieFcm.showKawaiiToast(title, body, clickUrl);

                            if (Notification.permission === 'granted') {
                                try {
                                    new Notification(title, {
                                        body: body,
                                        icon: '/favicon.png',
                                        data: { clickUrl: clickUrl }
                                    }).onclick = function (e) {
                                        e.preventDefault();
                                        window.focus();
                                        if (clickUrl && clickUrl !== '/') {
                                            window.location.href = clickUrl;
                                        }
                                    };
                                } catch (e) {
                                    console.warn('Native notification display error:', e);
                                }
                            }

                            // Notify Blazor C# component
                            if (dotNetRef) {
                                dotNetRef.invokeMethodAsync('OnFcmForegroundMessage', title, body, clickUrl);
                            }
                        });
                    }
                }

                return true;
            } catch (err) {
                console.warn('[BestieSpace FCM] Initialization error:', err);
                return false;
            }
        },

        requestToken: async function (vapidKey) {
            if (!window.bestieFcm.isSupported()) {
                return { success: false, error: 'Push notifications are not supported in this browser.' };
            }

            try {
                const permission = await Notification.requestPermission();
                if (permission !== 'granted') {
                    return { success: false, permission: permission, error: 'Notification permission denied.' };
                }

                if (!fcmMessaging && typeof firebase !== 'undefined') {
                    fcmMessaging = firebase.messaging();
                }

                if (!fcmMessaging) {
                    return { success: false, permission: 'granted', error: 'Firebase messaging not initialized.' };
                }

                const options = {
                    serviceWorkerRegistration: swRegistration
                };
                if (vapidKey && vapidKey.trim().length > 0) {
                    options.vapidKey = vapidKey.trim();
                }

                const currentToken = await fcmMessaging.getToken(options);
                if (currentToken) {
                    console.log('[BestieSpace FCM] Received device token (length ' + currentToken.length + ')');
                    return { success: true, token: currentToken, permission: 'granted' };
                } else {
                    return { success: false, error: 'No registration token available. Request permission to generate one.' };
                }
            } catch (err) {
                console.error('[BestieSpace FCM] An error occurred while retrieving token:', err);
                return { success: false, error: err.message || err.toString() };
            }
        },

        showKawaiiToast: function (title, body, clickUrl) {
            try {
                let toastContainer = document.getElementById('bestie-fcm-toast-container');
                if (!toastContainer) {
                    toastContainer = document.createElement('div');
                    toastContainer.id = 'bestie-fcm-toast-container';
                    toastContainer.className = 'fcm-toast-container';
                    document.body.appendChild(toastContainer);
                }

                const toast = document.createElement('div');
                toast.className = 'fcm-toast-item';
                toast.innerHTML = `
                    <div class="fcm-toast-icon">🌸</div>
                    <div class="fcm-toast-body">
                        <div class="fcm-toast-title">${title || 'BestieSpace'}</div>
                        <div class="fcm-toast-text">${body || ''}</div>
                    </div>
                    <button type="button" class="fcm-toast-close">&times;</button>
                `;

                toast.querySelector('.fcm-toast-close').onclick = (e) => {
                    e.stopPropagation();
                    toast.classList.add('hide');
                    setTimeout(() => toast.remove(), 300);
                };

                toast.onclick = () => {
                    if (clickUrl && clickUrl !== '/') {
                        window.location.href = clickUrl;
                    }
                    toast.remove();
                };

                toastContainer.appendChild(toast);

                setTimeout(() => {
                    if (toast.parentElement) {
                        toast.classList.add('hide');
                        setTimeout(() => toast.remove(), 300);
                    }
                }, 6000);
            } catch (e) {
                console.warn('Toast display error:', e);
            }
        }
    };
})();
