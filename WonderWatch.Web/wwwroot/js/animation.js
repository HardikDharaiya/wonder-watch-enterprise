/**
 * Wonder Watch - Global Animation & Interaction Controller
 * Architecture: Vanilla ES6 + IntersectionObserver + Anime.js (when available)
 * Motion Curve: easeOutExpo 700-900ms
 */

document.addEventListener('DOMContentLoaded', () => {

    // Detect Anime.js availability (loaded via CDN in _Layout.cshtml)
    const hasAnime = typeof window.anime !== 'undefined';

    // 1. PAGE LOAD FADE-IN (Anime.js enhanced)
    if (hasAnime) {
        document.body.style.opacity = '0';
        anime({
            targets: 'body',
            opacity: [0, 1],
            duration: 700,
            easing: 'easeOutExpo',
        });
    } else {
        document.body.style.opacity = '0';
        document.body.style.transition = 'opacity 600ms cubic-bezier(0.16, 1, 0.3, 1)';
        requestAnimationFrame(() => { document.body.style.opacity = '1'; });
    }

    // 2. NAVBAR SCROLL EFFECT
    const header = document.querySelector('header');
    if (header) {
        const handleScroll = () => {
            if (window.scrollY > 50) {
                header.classList.add('shadow-[0_10px_30px_rgba(0,0,0,0.5)]');
                header.style.backgroundColor = 'rgba(10, 10, 10, 0.98)';
                header.style.borderBottomColor = 'rgba(201, 167, 74, 0.2)';
            } else {
                header.classList.remove('shadow-[0_10px_30px_rgba(0,0,0,0.5)]');
                header.style.backgroundColor = 'transparent';
                header.style.borderBottomColor = 'transparent';
            }
        };
        window.addEventListener('scroll', handleScroll, { passive: true });
        handleScroll();
    }

    // 3. HERO ELEMENTS STAGGER (Anime.js powered — first section only)
    if (hasAnime) {
        const heroTargets = document.querySelectorAll(
            'section:first-of-type h1, section:first-of-type p, section:first-of-type a, section:first-of-type span.font-sans'
        );
        if (heroTargets.length > 0) {
            anime({
                targets: heroTargets,
                opacity: [0, 1],
                translateY: [30, 0],
                delay: anime.stagger(80, { start: 350 }),
                duration: 900,
                easing: 'easeOutExpo',
            });
        }
    }

    // 4. REVEAL ON SCROLL (IntersectionObserver + Anime.js)
    const revealElements = document.querySelectorAll('.group, section h2, section p, .bg-surface, .bg-surface-alt');
    if (revealElements.length > 0) {
        const filteredElements = Array.from(revealElements).filter(el =>
            !el.closest('header') && !el.closest('#cart-drawer') && !el.closest('aside')
        );

        // Set initial invisible state
        filteredElements.forEach(el => {
            el.style.opacity = '0';
            if (!hasAnime) {
                el.style.transform = 'translateY(20px)';
                el.style.transition = 'opacity 800ms cubic-bezier(0.16, 1, 0.3, 1), transform 800ms cubic-bezier(0.16, 1, 0.3, 1)';
            }
        });

        const revealObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const target = entry.target;
                    if (hasAnime) {
                        anime({
                            targets: target,
                            opacity: [0, 1],
                            translateY: [24, 0],
                            duration: 800,
                            easing: 'easeOutExpo',
                        });
                    } else {
                        target.style.opacity = '1';
                        target.style.transform = 'translateY(0)';
                    }
                    observer.unobserve(target);
                }
            });
        }, { threshold: 0.08, rootMargin: '0px 0px -40px 0px' });

        filteredElements.forEach(el => revealObserver.observe(el));
    }

    // 5. WATCH CARD GRID STAGGER (Anime.js — catalog/home grids)
    if (hasAnime) {
        const cardObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const grid = entry.target;
                    const cards = grid.querySelectorAll('.group');
                    if (cards.length > 0) {
                        anime({
                            targets: cards,
                            opacity: [0, 1],
                            translateY: [40, 0],
                            delay: anime.stagger(60),
                            duration: 700,
                            easing: 'easeOutExpo',
                        });
                    }
                    observer.unobserve(grid);
                }
            });
        }, { threshold: 0.05 });

        document.querySelectorAll('.grid').forEach(grid => {
            if (grid.querySelectorAll('.group').length > 1) {
                cardObserver.observe(grid);
            }
        });
    }

    // 6. GOLD PULSING TIMELINE DOTS (Anime.js — replaces Tailwind animate-ping)
    if (hasAnime) {
        const pingDots = document.querySelectorAll('.animate-ping');
        pingDots.forEach(dot => {
            dot.style.animation = 'none';
            anime({
                targets: dot,
                scale: [1, 2.5],
                opacity: [0.4, 0],
                duration: 1600,
                loop: true,
                easing: 'easeOutExpo',
            });
        });
    }

    // 7. INFINITE MARQUEE (Track-based rAF logic)
    const marquees = document.querySelectorAll('.js-marquee');
    marquees.forEach(marquee => {
        if (marquee.dataset.initialized === 'true') return;
        const originalContent = marquee.innerHTML;
        marquee.innerHTML = `
            <div class="marquee-track flex items-center whitespace-nowrap will-change-transform">
                <div class="marquee-content flex items-center">${originalContent}</div>
                <div class="marquee-content flex items-center">${originalContent}</div>
            </div>
        `;
        const track = marquee.querySelector('.marquee-track');
        const contentNodes = marquee.querySelectorAll('.marquee-content');
        contentNodes.forEach(node => {
            Array.from(node.children).forEach(child => child.classList.add('flex-shrink-0'));
        });
        let progress = 0;
        const speed = 0.8;
        const animate = () => {
            progress -= speed;
            const resetPoint = contentNodes[0].offsetWidth;
            if (Math.abs(progress) >= resetPoint) progress = 0;
            track.style.transform = `translateX(${progress}px)`;
            requestAnimationFrame(animate);
        };
        marquee.dataset.initialized = 'true';
        animate();
    });

    // 8. SMOOTH SCROLLING (96px header offset)
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const targetId = this.getAttribute('href');
            if (targetId === '#') return;
            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                e.preventDefault();
                const headerOffset = 96;
                const elementPosition = targetElement.getBoundingClientRect().top;
                const offsetPosition = elementPosition + window.pageYOffset - headerOffset;
                window.scrollTo({ top: offsetPosition, behavior: 'smooth' });
            }
        });
    });

});