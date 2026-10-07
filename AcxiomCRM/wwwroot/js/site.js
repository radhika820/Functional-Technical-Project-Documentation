(function () {
    'use strict';

    // Confirmation dialog for destructive actions: <form data-confirm="Message">
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-confirm')) return;
        if (form.dataset.confirmed === 'true') return;
        e.preventDefault();
        var modalEl = document.getElementById('confirmModal');
        if (!modalEl || !window.bootstrap) { if (window.confirm(form.getAttribute('data-confirm'))) form.submit(); return; }
        document.getElementById('confirmModalBody').textContent = form.getAttribute('data-confirm');
        var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        var ok = document.getElementById('confirmModalOk');
        var handler = function () {
            ok.removeEventListener('click', handler);
            form.dataset.confirmed = 'true';
            modal.hide();
            form.submit();
        };
        ok.addEventListener('click', handler);
        modalEl.addEventListener('hidden.bs.modal', function () { ok.removeEventListener('click', handler); }, { once: true });
        modal.show();
    });

    // Auto-dismiss success alerts
    window.setTimeout(function () {
        document.querySelectorAll('.alert-success').forEach(function (el) {
            if (window.bootstrap) bootstrap.Alert.getOrCreateInstance(el).close();
        });
    }, 6000);
})();
