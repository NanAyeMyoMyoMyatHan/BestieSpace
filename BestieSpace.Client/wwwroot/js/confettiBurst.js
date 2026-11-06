// Confetti & Floating Hearts Animation for BestieSpace
window.bestieAnimations = {
    triggerConfettiBurst: function () {
        const count = 45;
        const colors = ['#FF8FAB', '#FB6F92', '#FFC2D1', '#FFE5EC', '#E2F0CB', '#B5EAD7', '#FFDAC1', '#FFF'];
        const emojis = ['💖', '✨', '🌸', '🎀', '⭐', '💌', '🧸'];

        for (let i = 0; i < count; i++) {
            const particle = document.createElement('div');
            const isEmoji = Math.random() > 0.4;

            if (isEmoji) {
                particle.innerText = emojis[Math.floor(Math.random() * emojis.length)];
                particle.style.fontSize = `${16 + Math.random() * 20}px`;
            } else {
                particle.style.width = `${8 + Math.random() * 12}px`;
                particle.style.height = `${8 + Math.random() * 12}px`;
                particle.style.backgroundColor = colors[Math.floor(Math.random() * colors.length)];
                particle.style.borderRadius = Math.random() > 0.5 ? '50%' : '3px';
            }

            particle.style.position = 'fixed';
            particle.style.left = '50vw';
            particle.style.top = '50vh';
            particle.style.zIndex = '99999';
            particle.style.pointerEvents = 'none';
            particle.style.userSelect = 'none';

            // Random physics velocity
            const angle = Math.random() * Math.PI * 2;
            const velocity = 150 + Math.random() * 350;
            const tx = Math.cos(angle) * velocity;
            const ty = Math.sin(angle) * velocity - 100;
            const rot = (Math.random() - 0.5) * 720;

            particle.animate([
                { transform: 'translate(-50%, -50%) scale(0.2) rotate(0deg)', opacity: 1 },
                { transform: `translate(calc(-50% + ${tx}px), calc(-50% + ${ty}px)) scale(1.1) rotate(${rot}deg)`, opacity: 0.9, offset: 0.7 },
                { transform: `translate(calc(-50% + ${tx * 1.2}px), calc(-50% + ${ty + 200}px)) scale(0.6) rotate(${rot * 1.4}deg)`, opacity: 0 }
            ], {
                duration: 1800 + Math.random() * 800,
                easing: 'cubic-bezier(0.25, 1, 0.5, 1)',
                fill: 'forwards'
            });

            document.body.appendChild(particle);
            setTimeout(() => particle.remove(), 2600);
        }
    },

    spawnFloatingHearts: function (clickX, clickY) {
        const hearts = ['💖', '💕', '💗', '💓', '✨', '🌸', '🎀'];
        const num = 12;

        for (let i = 0; i < num; i++) {
            const heart = document.createElement('div');
            heart.innerText = hearts[Math.floor(Math.random() * hearts.length)];
            heart.style.position = 'fixed';
            heart.style.left = `${clickX || window.innerWidth / 2}px`;
            heart.style.top = `${clickY || window.innerHeight / 2}px`;
            heart.style.fontSize = `${20 + Math.random() * 24}px`;
            heart.style.zIndex = '99999';
            heart.style.pointerEvents = 'none';
            heart.style.userSelect = 'none';

            const driftX = (Math.random() - 0.5) * 200;
            const driftY = -(160 + Math.random() * 240);
            const scale = 0.8 + Math.random() * 0.8;

            heart.animate([
                { transform: 'translate(-50%, -50%) scale(0.4)', opacity: 1 },
                { transform: `translate(calc(-50% + ${driftX * 0.5}px), calc(-50% + ${driftY * 0.5}px)) scale(${scale})`, opacity: 0.9, offset: 0.6 },
                { transform: `translate(calc(-50% + ${driftX}px), calc(-50% + ${driftY}px)) scale(${scale * 1.2})`, opacity: 0 }
            ], {
                duration: 1600 + Math.random() * 600,
                easing: 'ease-out',
                fill: 'forwards'
            });

            document.body.appendChild(heart);
            setTimeout(() => heart.remove(), 2200);
        }
    },

    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    },

    playCutePopSound: function () {
        try {
            const AudioCtx = window.AudioContext || window.webkitAudioContext;
            if (!AudioCtx) return;
            const ctx = new AudioCtx();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();

            osc.type = 'sine';
            const now = ctx.currentTime;
            // Cute bubbly chime sweep: 540Hz to 1080Hz
            osc.frequency.setValueAtTime(540, now);
            osc.frequency.exponentialRampToValueAtTime(1080, now + 0.12);

            gain.gain.setValueAtTime(0.2, now);
            gain.gain.exponentialRampToValueAtTime(0.001, now + 0.14);

            osc.connect(gain);
            gain.connect(ctx.destination);

            osc.start(now);
            osc.stop(now + 0.15);
        } catch (e) {
            // AudioContext gesture error, ignore
        }
    }
};

// Bestie Auth LocalStorage Helper
window.bestieAuth = {
    getSession: function () {
        try {
            return localStorage.getItem('bestie_user_session');
        } catch (e) {
            return null;
        }
    },
    setSession: function (json) {
        try {
            localStorage.setItem('bestie_user_session', json);
        } catch (e) {}
    },
    clearSession: function () {
        try {
            localStorage.removeItem('bestie_user_session');
        } catch (e) {}
    }
};

// Bestie VIP Ticket Print & Copy Helpers
window.bestieTicket = {
    copyText: async function (text) {
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text);
                return true;
            }
            const textarea = document.createElement("textarea");
            textarea.value = text;
            textarea.style.position = "fixed";
            textarea.style.opacity = "0";
            document.body.appendChild(textarea);
            textarea.select();
            document.execCommand("copy");
            document.body.removeChild(textarea);
            return true;
        } catch (e) {
            return false;
        }
    },
    printTicket: function () {
        window.print();
    }
};
