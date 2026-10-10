// Landing page behaviour (kept as a file so the Content-Security-Policy can forbid inline scripts).
  // ---- Settings ----
  var WHATSAPP_NUMBER = '919670226608';   // country code + number, digits only; '' hides the WhatsApp button
  var SPOTS_TOTAL = 5;                    // free-setup offer: lower SPOTS_LEFT as restaurants join
  var SPOTS_LEFT = 5;

  var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  document.querySelectorAll('[data-year]').forEach(function (el) { el.textContent = String(new Date().getFullYear()); });

  // Navbar: blur + border after scrolling.
  var nav = document.getElementById('nav');
  function onScroll() { nav.classList.toggle('scrolled', window.scrollY > 8); }
  window.addEventListener('scroll', onScroll, { passive: true });
  onScroll();

  // Mobile menu.
  var burger = document.getElementById('burger');
  var menu = document.getElementById('mobile-menu');
  function setMenu(open) {
    menu.classList.toggle('open', open);
    burger.setAttribute('aria-expanded', String(open));
    burger.setAttribute('aria-label', open ? 'Close menu' : 'Open menu');
  }
  burger.addEventListener('click', function () { setMenu(!menu.classList.contains('open')); });
  menu.querySelectorAll('a').forEach(function (a) { a.addEventListener('click', function () { setMenu(false); }); });
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape') setMenu(false); });

  // Stagger index for reveal children that sit together in a grid/list.
  document.querySelectorAll('.features, .flow4, .xf, .stats, .plans, .qr, .needs, .faq').forEach(function (group) {
    Array.prototype.forEach.call(group.children, function (child, i) { child.style.setProperty('--i', String(i)); });
  });

  // Indian number format for counters.
  function inr(n) { return Math.round(n).toLocaleString('en-IN'); }
  function countUp(el) {
    var target = Number(el.getAttribute('data-count'));
    var prefix = el.getAttribute('data-prefix') || '';
    if (reduceMotion || !target) { el.textContent = prefix + inr(target); return; }
    var start = performance.now(), dur = 1100;
    function tick(now) {
      var t = Math.min(1, (now - start) / dur);
      var eased = 1 - Math.pow(1 - t, 3);
      el.textContent = prefix + inr(target * eased);
      if (t < 1) requestAnimationFrame(tick);
    }
    el.textContent = prefix + '0';
    requestAnimationFrame(tick);
  }

  // Scroll reveal, chart/bar animation and counters.
  var observed = document.querySelectorAll('.reveal, .reveal-x, .xf-row, .stage, .bars, .anywhere');
  if ('IntersectionObserver' in window) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('in');
        entry.target.querySelectorAll('[data-count]').forEach(countUp);
        // Once revealed, drop the stagger so hover reacts at once.
        var el = entry.target;
        setTimeout(function () { el.style.setProperty('--i', '0'); }, 1600);
        io.unobserve(entry.target);
      });
    }, { threshold: 0.18, rootMargin: '0px 0px -40px 0px' });
    observed.forEach(function (el) { io.observe(el); });
  } else {
    observed.forEach(function (el) { el.classList.add('in'); });
  }
  // The hero mockup is already on screen: start its chart after the load-in.
  setTimeout(function () {
    var stage = document.querySelector('.stage');
    if (stage && !stage.classList.contains('in')) { stage.classList.add('in'); stage.querySelectorAll('[data-count]').forEach(countUp); }
  }, 700);

  // Button ripple.
  document.querySelectorAll('.btn').forEach(function (btn) {
    btn.addEventListener('pointerdown', function (e) {
      if (reduceMotion) return;
      var rect = btn.getBoundingClientRect();
      var size = Math.max(rect.width, rect.height);
      var r = document.createElement('span');
      r.className = 'ripple';
      r.style.width = r.style.height = size + 'px';
      r.style.left = (e.clientX - rect.left - size / 2) + 'px';
      r.style.top = (e.clientY - rect.top - size / 2) + 'px';
      btn.appendChild(r);
      setTimeout(function () { r.remove(); }, 650);
    });
  });

  // FAQ: one answer open at a time.
  var qas = document.querySelectorAll('.qa');
  qas.forEach(function (qa) {
    var button = qa.querySelector('button');
    button.addEventListener('click', function () {
      var opening = !qa.hasAttribute('data-open');
      qas.forEach(function (other) { other.removeAttribute('data-open'); other.querySelector('button').setAttribute('aria-expanded', 'false'); });
      if (opening) { qa.setAttribute('data-open', ''); button.setAttribute('aria-expanded', 'true'); }
    });
  });

  // Free-setup spots.
  (function () {
    var left = Math.max(0, Math.min(SPOTS_TOTAL, SPOTS_LEFT));
    document.querySelectorAll('[data-spots-left]').forEach(function (el) { el.textContent = String(left); });
    document.querySelectorAll('[data-spots-total]').forEach(function (el) { el.textContent = String(SPOTS_TOTAL); });
    var dots = document.querySelector('[data-dots]');
    for (var i = 0; dots && i < SPOTS_TOTAL; i++) {
      var d = document.createElement('span');
      d.className = 'dot' + (i < SPOTS_TOTAL - left ? ' taken' : '');
      dots.appendChild(d);
    }
    if (WHATSAPP_NUMBER) {
      document.getElementById('phone-text').textContent = '+' + WHATSAPP_NUMBER.replace(/^(\d{2})(\d{5})(\d+)$/, '$1 $2 $3');
    }
  })();

  // Plan buttons pre-select the plan in the form.
  document.querySelectorAll('[data-plan]').forEach(function (link) {
    link.addEventListener('click', function () { document.getElementById('lf-plan').value = link.getAttribute('data-plan'); });
  });

  function copyText(text, button, onFail) {
    var done = function () { var old = button.textContent; button.textContent = 'Copied'; setTimeout(function () { button.textContent = old; }, 1800); };
    try { navigator.clipboard.writeText(text).then(done, onFail); } catch (e) { onFail(); }
  }
  function selectNode(el) {
    var range = document.createRange(); range.selectNodeContents(el);
    var sel = window.getSelection(); sel.removeAllRanges(); sel.addRange(range);
  }
  document.querySelectorAll('[data-copy]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var el = document.getElementById(btn.getAttribute('data-copy'));
      copyText(el.textContent.trim(), btn, function () { selectNode(el); });
    });
  });

  // Trial form: builds a WhatsApp message; nothing is stored or sent from this page.
  var form = document.getElementById('lead-form');
  var out = document.getElementById('lf-out');
  if (!WHATSAPP_NUMBER) {
    document.getElementById('lf-whatsapp').hidden = true;
    document.getElementById('lf-note').textContent = 'Copy the message and send it to us on WhatsApp or SMS.';
  }
  function buildMessage() {
    var v = function (id) { return document.getElementById(id).value.trim(); };
    var nameEl = document.getElementById('lf-name'), restEl = document.getElementById('lf-restaurant');
    var name = v('lf-name'), restaurant = v('lf-restaurant');
    var error = document.getElementById('lf-error');
    nameEl.setAttribute('aria-invalid', String(!name));
    restEl.setAttribute('aria-invalid', String(!restaurant));
    if (!name || !restaurant) { error.hidden = false; (name ? restEl : nameEl).focus(); return null; }
    error.hidden = true;
    var lines = ['Hi QRenvo, I want to start the 3-day free trial.', 'Name: ' + name, 'Restaurant: ' + restaurant];
    if (v('lf-city')) lines.push('City: ' + v('lf-city'));
    if (v('lf-tables')) lines.push('Tables: ' + v('lf-tables'));
    lines.push('Plan: ' + v('lf-plan'));
    return lines.join('\n');
  }
  form.addEventListener('submit', function (event) {
    event.preventDefault();
    var message = buildMessage();
    if (!message || !WHATSAPP_NUMBER) return;
    var link = document.createElement('a');
    link.href = 'https://wa.me/' + WHATSAPP_NUMBER + '?text=' + encodeURIComponent(message);
    link.target = '_blank'; link.rel = 'noopener';
    document.body.appendChild(link); link.click(); link.remove();
  });
  document.getElementById('lf-copy').addEventListener('click', function (event) {
    var message = buildMessage();
    if (!message) return;
    out.textContent = message; out.hidden = false;
    copyText(message, event.currentTarget, function () { selectNode(out); });
  });

  // ---- Testimonials ----
  // Published ratings from app.qrenvo.com. Text goes in with textContent only, never as HTML.
  (function () {
    var local = /^(localhost|127\.0\.0\.1)$/.test(location.hostname);
    var apiBase = local ? (new URLSearchParams(location.search).get('api') || 'http://localhost:5176') : 'https://app.qrenvo.com';
    var section = document.getElementById('testimonials');
    var list = document.getElementById('tm-list');
    if (!section || !list || !window.fetch) return;

    function el(tag, cls, text) {
      var n = document.createElement(tag);
      if (cls) n.className = cls;
      if (text != null) n.textContent = text;
      return n;
    }
    function safeImage(url) {
      return typeof url === 'string' && /^https:\/\/(app|demo)\.qrenvo\.com\/uploads\//.test(url) || (local && /^http:\/\/localhost:\d+\/uploads\//.test(url));
    }

    fetch(apiBase + '/api/public/testimonials', { credentials: 'omit' })
      .then(function (r) { return r.ok ? r.json() : []; })
      .then(function (items) {
        if (!Array.isArray(items) || items.length === 0) return;
        items.slice(0, 12).forEach(function (t, i) {
          var card = el('article', 'card');
          card.style.setProperty('--i', String(i));
          var rating = Math.max(1, Math.min(5, Number(t.rating) || 5));
          var stars = el('div', 'tm-stars', '★★★★★'.slice(0, rating) + '☆☆☆☆☆'.slice(0, 5 - rating));
          stars.setAttribute('aria-label', rating + ' out of 5 stars');
          card.appendChild(stars);
          if (t.comment) card.appendChild(el('p', 'tm-quote', String(t.comment)));

          var who = el('div', 'tm-who');
          var name = String(t.name || 'Guest');
          if (safeImage(t.imageUrl)) {
            var img = el('img', 'tm-avatar');
            img.src = t.imageUrl; img.alt = ''; img.width = 44; img.height = 44; img.loading = 'lazy';
            img.onerror = function () { img.replaceWith(el('span', 'tm-avatar tm-letter', name.charAt(0).toUpperCase())); };
            who.appendChild(img);
          } else {
            who.appendChild(el('span', 'tm-avatar tm-letter', name.charAt(0).toUpperCase()));
          }
          var text = el('div');
          text.appendChild(el('div', 'tm-name', name));
          text.appendChild(el('div', 'tm-role', String(t.role || '')));
          who.appendChild(text);
          card.appendChild(who);
          list.appendChild(card);
        });
        section.hidden = false;
      })
      .catch(function () { /* No testimonials: the section simply stays hidden. */ });
  })();
