// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
(function ($) {
    document.body.classList.add('is-loading');
    window.addEventListener('load', function () {
        window.setTimeout(function () {
            document.body.classList.remove('is-loading');
        }, 250);
    });

    const sidebarSelector = '#appSidebar';
    const layoutSelector = '#layoutShell';
    const menuTreeHostSelector = '#menuTreeHost';
    const parentMenuSelectSelector = '#parentMenuSelect';

    $(document).on('click', '[data-sidebar-toggle]', function () {
        $(sidebarSelector).toggleClass('is-visible');
        $(layoutSelector).toggleClass('is-sidebar-open');
    });

    $(document).on('click', '[data-sidebar-close]', function () {
        $(sidebarSelector).removeClass('is-visible');
        $(layoutSelector).removeClass('is-sidebar-open');
    });

    function showToast(message, type) {
        const toastEl = document.getElementById('appToast');
        if (!toastEl || !message) {
            return;
        }

        toastEl.classList.remove('toast-success', 'toast-danger', 'toast-warning', 'toast-info');
        toastEl.classList.add('toast-' + (type || 'success'));
        const body = toastEl.querySelector('.toast-body');
        if (body) {
            body.textContent = message;
        }

        const toast = bootstrap.Toast.getOrCreateInstance(toastEl, { delay: 3500 });
        toast.show();
    }

    window.showToast = showToast;

    (function initToast() {
        const toastNodes = document.querySelectorAll('.toast-data');
        if (!toastNodes.length) {
            return;
        }
        toastNodes.forEach(node => {
            const message = node.getAttribute('data-message');
            const type = node.getAttribute('data-type');
            showToast(message, type);
        });
    })();

    const themeKey = 'one_db_theme';
    const themes = ['green-blue', 'navy-gold', 'charcoal-teal', 'slate-amber'];

    function applyTheme(theme) {
        const html = document.documentElement;
        html.setAttribute('data-theme', theme);
        localStorage.setItem(themeKey, theme);
    }

    function initTheme() {
        const userTheme = window.appUserTheme;
        if (userTheme && themes.includes(userTheme)) {
            applyTheme(userTheme);
            return;
        }

        const saved = localStorage.getItem(themeKey);
        const theme = themes.includes(saved) ? saved : 'green-blue';
        applyTheme(theme);
    }

    initTheme();

    $(document).on('click', '#themeToggle', function () {
        const html = document.documentElement;
        const current = html.getAttribute('data-theme');
        const index = themes.indexOf(current);
        const next = themes[(index + 1) % themes.length];
        applyTheme(next);
    });

    $(document).on('change', '#toggleHiddenMenu', function () {
        const showHidden = $(this).is(':checked');
        $('.menu-node.is-hidden').toggleClass('menu-node-hidden', !showHidden);
    });

    function refreshMenuTree() {
        const host = $(menuTreeHostSelector);
        if (!host.length) {
            return;
        }

        host.load('/MenuAdmin/MenuTreePartial', function () {
            const toggle = $('#toggleHiddenMenu');
            if (toggle.length && !toggle.is(':checked')) {
                $('.menu-node.is-hidden', host).addClass('menu-node-hidden');
            }
        });
    }

    function refreshParentMenuOptions() {
        const select = $(parentMenuSelectSelector);
        if (!select.length) {
            return;
        }

        $.get('/MenuAdmin/ParentMenuOptions')
            .done(function (html) {
                select.find('option:not(:first)').remove();
                select.append(html);
            });
    }

    function showAlert(message, type) {
        showToast(message, type);
    }

    function wireAjaxForm(selector) {
        $(document).on('submit', selector, function (event) {
            event.preventDefault();
            const form = $(this);
            const submitButton = form.find('[type="submit"]');
            const token = form.find('input[name="__RequestVerificationToken"]').val();

            submitButton.prop('disabled', true);

            if (typeof form.valid === 'function' && !form.valid()) {
                submitButton.prop('disabled', false);
                return;
            }

            $.ajax({
                url: form.attr('action'),
                method: 'POST',
                data: form.serialize(),
                headers: token ? { 'RequestVerificationToken': token } : undefined
            })
                .done(function (response) {
                    showAlert(response.message || 'Berhasil disimpan.', 'success');
                    form.trigger('reset');
                    refreshMenuTree();
                    refreshParentMenuOptions();
                })
                .fail(function (xhr) {
                    const message = xhr.responseJSON && xhr.responseJSON.message
                        ? xhr.responseJSON.message
                        : 'Terjadi kesalahan. Mohon periksa kembali input Anda.';
                    showAlert(message, 'danger');
                })
                .always(function () {
                    submitButton.prop('disabled', false);
                });
        });
    }

    $(function () {
        const toggleHidden = $('#toggleHiddenMenu');
        if (toggleHidden.length) {
            toggleHidden.trigger('change');
        }

        wireAjaxForm('#formAddMenu');
        wireAjaxForm('#formAddSubMenu');
    });

    (function initGlobalSearch() {
        const input = document.getElementById('globalSearchInput');
        const dropdown = document.getElementById('globalSearchDropdown');
        if (!input || !dropdown) {
            return;
        }

        let timer = null;

        function clearResults() {
            dropdown.innerHTML = '';
            dropdown.classList.remove('is-visible');
        }

        function renderResults(items) {
            dropdown.innerHTML = '';
            if (!items || items.length === 0) {
                dropdown.innerHTML = '<div class="search-empty">Tidak ada hasil.</div>';
                dropdown.classList.add('is-visible');
                return;
            }

            items.forEach(item => {
                const row = document.createElement('a');
                row.className = 'search-item';
                row.href = item.url || '#';
                const label = document.createElement('span');
                label.appendChild(highlightText(item.label || '', input.value.trim()));
                const category = document.createElement('small');
                category.textContent = item.category;
                row.appendChild(label);
                row.appendChild(category);
                dropdown.appendChild(row);
            });
            dropdown.classList.add('is-visible');
        }

        function highlightText(text, query) {
            const fragment = document.createDocumentFragment();
            if (!query) {
                fragment.appendChild(document.createTextNode(text));
                return fragment;
            }

            const lowerText = text.toLowerCase();
            const lowerQuery = query.toLowerCase();
            let index = 0;

            while (true) {
                const found = lowerText.indexOf(lowerQuery, index);
                if (found === -1) {
                    fragment.appendChild(document.createTextNode(text.substring(index)));
                    break;
                }

                if (found > index) {
                    fragment.appendChild(document.createTextNode(text.substring(index, found)));
                }

                const mark = document.createElement('mark');
                mark.textContent = text.substring(found, found + query.length);
                fragment.appendChild(mark);
                index = found + query.length;
            }

            return fragment;
        }

        input.addEventListener('input', function () {
            const value = input.value.trim();
            window.clearTimeout(timer);
            if (value.length < 2) {
                clearResults();
                return;
            }

            timer = window.setTimeout(function () {
                fetch(`/Search/Quick?q=${encodeURIComponent(value)}`)
                    .then(res => res.json())
                    .then(data => renderResults(data.items || []))
                    .catch(() => clearResults());
            }, 200);
        });

        document.addEventListener('click', function (event) {
            if (!dropdown.contains(event.target) && event.target !== input) {
                dropdown.classList.remove('is-visible');
            }
        });
    })();

    (function initCommandPalette() {
        const palette = document.getElementById('commandPalette');
        const input = document.getElementById('commandPaletteInput');
        const results = document.getElementById('commandPaletteResults');
        if (!palette || !input || !results) {
            return;
        }

        let items = [];
        let activeIndex = -1;
        let timer = null;

        function openPalette() {
            palette.classList.add('is-open');
            palette.setAttribute('aria-hidden', 'false');
            input.value = '';
            results.innerHTML = '';
            items = [];
            activeIndex = -1;
            setTimeout(() => input.focus(), 50);
        }

        function closePalette() {
            palette.classList.remove('is-open');
            palette.setAttribute('aria-hidden', 'true');
            results.innerHTML = '';
        }

        function highlightText(text, query) {
            if (!query) {
                return text;
            }

            const regex = new RegExp(`(${query})`, 'ig');
            return text.replace(regex, '<mark>$1</mark>');
        }

        function renderResults(list, query) {
            results.innerHTML = '';
            if (!list || list.length === 0) {
                results.innerHTML = '<div class="text-secondary small px-2">Tidak ada hasil.</div>';
                return;
            }

            list.forEach((item, index) => {
                const row = document.createElement('a');
                row.className = 'command-item' + (index === activeIndex ? ' is-active' : '');
                row.href = item.url || '#';
                row.innerHTML = `<span>${highlightText(item.label || '', query)}</span><small>${item.category || ''}</small>`;
                row.addEventListener('mouseenter', () => {
                    activeIndex = index;
                    updateActive();
                });
                row.addEventListener('click', () => closePalette());
                results.appendChild(row);
            });
        }

        function updateActive() {
            const rows = results.querySelectorAll('.command-item');
            rows.forEach((row, idx) => {
                row.classList.toggle('is-active', idx === activeIndex);
            });
        }

        function moveActive(delta) {
            if (items.length === 0) {
                activeIndex = -1;
                return;
            }
            activeIndex = (activeIndex + delta + items.length) % items.length;
            updateActive();
            const activeRow = results.querySelector('.command-item.is-active');
            if (activeRow) {
                activeRow.scrollIntoView({ block: 'nearest' });
            }
        }

        function openActive() {
            if (activeIndex < 0 || activeIndex >= items.length) {
                return;
            }
            const target = items[activeIndex];
            if (target.url) {
                window.location.href = target.url;
            }
        }

        input.addEventListener('input', function () {
            const value = input.value.trim();
            window.clearTimeout(timer);
            if (value.length < 2) {
                results.innerHTML = '<div class="text-secondary small px-2">Ketik minimal 2 karakter.</div>';
                items = [];
                activeIndex = -1;
                return;
            }

            timer = window.setTimeout(function () {
                fetch(`/Search/Quick?q=${encodeURIComponent(value)}`)
                    .then(res => res.json())
                    .then(data => {
                        items = data.items || [];
                        activeIndex = items.length ? 0 : -1;
                        renderResults(items, value);
                        updateActive();
                    })
                    .catch(() => {
                        results.innerHTML = '<div class="text-secondary small px-2">Gagal memuat hasil.</div>';
                    });
            }, 150);
        });

        input.addEventListener('keydown', function (event) {
            if (event.key === 'ArrowDown') {
                event.preventDefault();
                moveActive(1);
            } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                moveActive(-1);
            } else if (event.key === 'Enter') {
                event.preventDefault();
                openActive();
            } else if (event.key === 'Escape') {
                closePalette();
            }
        });

        document.addEventListener('keydown', function (event) {
            if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
                event.preventDefault();
                if (palette.classList.contains('is-open')) {
                    closePalette();
                } else {
                    openPalette();
                }
            }
            if (event.key === 'Escape' && palette.classList.contains('is-open')) {
                closePalette();
            }
        });

        document.querySelectorAll('[data-command-close]').forEach(btn => {
            btn.addEventListener('click', closePalette);
        });
    })();

    (function initCompanySelects() {
        if (!$.fn.select2) {
            return;
        }

        const selects = $('.js-company-select');
        if (!selects.length) {
            return;
        }

        selects.each(function () {
            const $select = $(this);
            if ($select.data('select2')) {
                return;
            }
            $select.select2({
                width: '100%',
                placeholder: $select.data('placeholder') || 'Pilih perusahaan',
                allowClear: true
            });
        });
    })();

    (function initRealtimeNotifications() {
        if (typeof signalR === 'undefined') {
            return;
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/notifications')
            .withAutomaticReconnect()
            .build();

        connection.on('notify', function (payload) {
            if (!payload) return;
            showToast(payload.message, payload.type || 'info');
        });

        connection.start().catch(function () {
            // ignore connection errors
        });
    })();
})(jQuery);
