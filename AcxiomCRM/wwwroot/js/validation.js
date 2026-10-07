// Client-side validation adapters (jquery-validate / unobtrusive).
// Server-side validation is always repeated; this only gives immediate feedback.
(function ($) {
    'use strict';
    if (!$ || !$.validator) return;

    function parseDate(value) {
        var parts = (value || '').split('-');
        if (parts.length !== 3) return null;
        return new Date(parseInt(parts[0], 10), parseInt(parts[1], 10) - 1, parseInt(parts[2], 10));
    }

    // "notpast": date must be today or later, unless another field holds an exempt value.
    $.validator.addMethod('notpast', function (value, element, params) {
        if (!value) return true;
        if (params && params.exempt) {
            var other = $(element.form).find('[name="' + params.exempt + '"]');
            var current = other.length ? other.val() : null;
            if (current && params.values && params.values.split(',').indexOf(current) >= 0) return true;
        }
        var d = parseDate(value);
        if (!d || isNaN(d.getTime())) return false;
        var today = new Date();
        today.setHours(0, 0, 0, 0);
        return d >= today;
    });

    $.validator.unobtrusive.adapters.add('notpast', ['exempt', 'values'], function (options) {
        options.rules['notpast'] = { exempt: options.params.exempt, values: options.params.values };
        options.messages['notpast'] = options.message;
    });

    // Re-validate the date when the field it depends on changes (e.g. Stage / Status).
    $(function () {
        $('input[data-val-notpast-exempt]').each(function () {
            var input = $(this);
            $(input[0].form).find('[name="' + input.attr('data-val-notpast-exempt') + '"]').on('change', function () {
                input.valid();
            });
        });
    });
})(window.jQuery);
