(function () {
    'use strict';

    var STORAGE_KEY = 'hourtrack.signature.v1';
    var MAX_UPLOAD_BYTES = 2 * 1024 * 1024;

    function storage() {
        try { return window.localStorage; } catch (e) { return null; }
    }

    function init(root) {
        if (root._sigPad) return root._sigPad;

        var canvas = root.querySelector('.sig-pad-canvas');
        var ctx = canvas.getContext('2d');
        var valueInput = root.querySelector('[data-sig-value]');
        var errorEl = root.querySelector('[data-sig-error]');
        var stage = root.querySelector('.sig-pad-stage');
        var uploadBox = root.querySelector('.sig-pad-upload');
        var fileInput = root.querySelector('[data-sig-file]');
        var rememberBox = root.querySelector('[data-sig-remember]');
        var useSavedBtn = root.querySelector('[data-sig-use-saved]');
        var drawing = false, last = null, hasInk = false;

        function setupContext() {
            ctx.lineWidth = 2.6;
            ctx.lineCap = 'round';
            ctx.lineJoin = 'round';
            ctx.strokeStyle = '#111827';
        }
        setupContext();

        function point(e) {
            var r = canvas.getBoundingClientRect();
            return {
                x: (e.clientX - r.left) * (canvas.width / r.width),
                y: (e.clientY - r.top) * (canvas.height / r.height)
            };
        }

        function commit() {
            valueInput.value = hasInk ? canvas.toDataURL('image/png') : '';
            root.classList.toggle('has-ink', hasInk);
            if (hasInk) {
                root.classList.remove('is-invalid');
                errorEl.classList.add('d-none');
            }
            var s = storage();
            if (s && rememberBox && rememberBox.checked) {
                if (hasInk) s.setItem(STORAGE_KEY, valueInput.value);
            }
        }

        function clear() {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            hasInk = false;
            commit();
        }

        canvas.addEventListener('pointerdown', function (e) {
            if (e.pointerType === 'mouse' && e.button !== 0) return;
            drawing = true;
            last = point(e);
            canvas.setPointerCapture(e.pointerId);
            // A tap leaves a dot rather than nothing.
            ctx.beginPath();
            ctx.arc(last.x, last.y, ctx.lineWidth / 2, 0, Math.PI * 2);
            ctx.fillStyle = ctx.strokeStyle;
            ctx.fill();
            hasInk = true;
            e.preventDefault();
        });

        canvas.addEventListener('pointermove', function (e) {
            if (!drawing) return;
            var p = point(e);
            var mid = { x: (last.x + p.x) / 2, y: (last.y + p.y) / 2 };
            ctx.beginPath();
            ctx.moveTo(last.x, last.y);
            ctx.quadraticCurveTo(last.x, last.y, mid.x, mid.y);
            ctx.stroke();
            last = p;
            e.preventDefault();
        });

        function endStroke() {
            if (!drawing) return;
            drawing = false;
            commit();
        }
        canvas.addEventListener('pointerup', endStroke);
        canvas.addEventListener('pointercancel', endStroke);

        function drawImageFit(img) {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            var pad = 10;
            var scale = Math.min((canvas.width - pad * 2) / img.width, (canvas.height - pad * 2) / img.height, 1);
            var w = img.width * scale, h = img.height * scale;
            ctx.drawImage(img, (canvas.width - w) / 2, (canvas.height - h) / 2, w, h);
            hasInk = true;
            commit();
        }

        function loadFromSrc(src) {
            var img = new Image();
            img.onload = function () { drawImageFit(img); };
            img.src = src;
        }

        // Switches between drawing on the pad and uploading a picture of a signature.
        root.querySelectorAll('[data-sig-mode]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var upload = btn.getAttribute('data-sig-mode') === 'upload';
                root.querySelectorAll('[data-sig-mode]').forEach(function (b) { b.classList.toggle('active', b === btn); });
                uploadBox.classList.toggle('d-none', !upload);
                stage.classList.toggle('d-none', false);
            });
        });

        fileInput.addEventListener('change', function () {
            var file = fileInput.files && fileInput.files[0];
            if (!file) return;
            if (!/^image\/(png|jpeg)$/.test(file.type) || file.size > MAX_UPLOAD_BYTES) {
                errorEl.textContent = 'Please choose a PNG or JPG image up to 2 MB.';
                errorEl.classList.remove('d-none');
                fileInput.value = '';
                return;
            }
            errorEl.classList.add('d-none');
            var url = URL.createObjectURL(file);
            var img = new Image();
            img.onload = function () { drawImageFit(img); URL.revokeObjectURL(url); };
            img.onerror = function () { URL.revokeObjectURL(url); };
            img.src = url;
        });

        root.querySelector('[data-sig-clear]').addEventListener('click', function () {
            fileInput.value = '';
            clear();
        });

        var s = storage();
        if (s && s.getItem(STORAGE_KEY) && useSavedBtn) {
            useSavedBtn.classList.remove('d-none');
            useSavedBtn.addEventListener('click', function () { loadFromSrc(s.getItem(STORAGE_KEY)); });
        }
        if (rememberBox) {
            rememberBox.addEventListener('change', function () {
                var st = storage();
                if (!st) return;
                if (rememberBox.checked && hasInk) st.setItem(STORAGE_KEY, valueInput.value);
                if (!rememberBox.checked) st.removeItem(STORAGE_KEY);
            });
        }

        var api = {
            isEmpty: function () { return !hasInk; },
            value: function () { return valueInput.value; },
            clear: clear,
            markInvalid: function (message) {
                root.classList.add('is-invalid');
                errorEl.textContent = message || 'Please sign before continuing.';
                errorEl.classList.remove('d-none');
            }
        };
        root._sigPad = api;
        return api;
    }

    function initAll(scope) {
        (scope || document).querySelectorAll('[data-sig-pad]').forEach(init);
    }

    window.SignaturePads = {
        init: initAll,
        // Returns false (and flags the pad) if any pad inside `container` hasn't been signed yet.
        validate: function (container) {
            var ok = true;
            (container || document).querySelectorAll('[data-sig-pad]').forEach(function (root) {
                var pad = init(root);
                if (pad.isEmpty()) { pad.markInvalid(); ok = false; }
            });
            return ok;
        },
        clearAll: function (container) {
            (container || document).querySelectorAll('[data-sig-pad]').forEach(function (root) { init(root).clear(); });
        },
        valueOf: function (container) {
            var root = (container || document).querySelector('[data-sig-pad]');
            return root ? init(root).value() : '';
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { initAll(); });
    } else {
        initAll();
    }
})();
