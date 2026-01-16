/* Login page interactions, extracted from Razor for maintainability */
(function ($) {
    'use strict';

    var config = window.LoginConfig || {};
    var endpoints = config.endpoints || {};
    var profilePlaceholder = config.profilePlaceholder || '';
    var onlineIntervalMs = config.onlineIntervalMs || 60000;
    var minPasswordLength = config.minPasswordLength || 6;
    var forgotVerifiedNrp = '';
    var nrpCacheKey = 'last_nrp_login';

    function loadCachedNrp() {
        try {
            var cached = localStorage.getItem(nrpCacheKey);
            if (cached) {
                $('#loginNrp').val(cached);
            }
        } catch (e) { /* ignore */ }
    }

    function cacheNrp(nrp) {
        try {
            if (nrp) localStorage.setItem(nrpCacheKey, nrp);
        } catch (e) { /* ignore */ }
    }

    function getToken() {
        var selector = config.tokenFieldSelector || 'input[name="__RequestVerificationToken"]';
        var el = document.querySelector(selector);
        return el ? el.value : '';
    }

    $.ajaxSetup({
        headers: {
            'RequestVerificationToken': getToken(),
            'X-Requested-With': 'XMLHttpRequest'
        }
    });

    function withCsrf(options) {
        var token = getToken();
        options = options || {};
        options.headers = options.headers || {};
        options.headers['X-Requested-With'] = 'XMLHttpRequest';
        if (token) {
            options.headers['RequestVerificationToken'] = token;
        }
        return options;
    }

    function resetCategoryResult() {
        $('#categoryResultPanel').addClass('d-none');
        $('#categoryResultInfo').text('');
        $('#categoryResults').empty();
    }

    function renderCategoryResults(records, nrp) {
        var panel = $('#categoryResultPanel');
        var info = $('#categoryResultInfo');
        var list = $('#categoryResults');
        list.empty();

        if (!records.length) {
            info.text('NRP ' + nrp + ' belum memiliki kategori aktif di sistem.');
            panel.removeClass('d-none');
            return;
        }

        var message = records.length === 1
            ? '1 kategori ditemukan untuk NRP ' + nrp + '.'
            : records.length + ' kategori ditemukan untuk NRP ' + nrp + '.';
        info.text(message + ' Pilih kategori yang ingin digunakan untuk masuk.');

        records.forEach(function (entry) {
            var photo = entry.profile_photo || profilePlaceholder;
            var controller = entry.login_controller || 'Menu';
            var func = entry.login_function || 'Index';
            var card = $('<div>').addClass('category-result-card');
            var header = $('<div>').addClass('d-flex justify-content-between align-items-start flex-wrap gap-2');
            var left = $('<div>').addClass('d-flex align-items-center gap-2');
            left.append($('<img>').addClass('rounded-circle mr-2').attr({ src: photo, width: 32, height: 32 }));
            left.append($('<div>').addClass('category-result-title').text(entry.kategori || 'Kategori tidak dikenal'));
            header.append(left);
            header.append($('<span>').addClass('badge badge-pill category-result-badge').text(entry.kategori_user_id || 'ID tidak tersedia'));
            card.append(header);

            var ownerText = entry.nama ? 'Nama: ' + entry.nama : 'Nama tidak tersedia';
            card.append($('<p>').addClass('mb-1 small category-result-meta').text(ownerText));

            var tags = $('<div>').addClass('category-result-tags mt-2');
            tags.append($('<span>').addClass('badge badge-light border').text('Dept: ' + (entry.dept_name || 'Tidak tersedia')));
            tags.append($('<span>').addClass('badge badge-light border').text('Company: ' + (entry.nama_company || 'Tidak tersedia')));
            card.append(tags);

            var menuLine = $('<p>').addClass('mt-3 mb-2 small category-result-menu');
            menuLine.append('Menu awal: ');
            menuLine.append($('<strong>').text(controller + '/' + func));
            card.append(menuLine);

            var action = $('<div>').addClass('d-flex justify-content-md-end');
            var selectBtn = $('<button>')
                .addClass('btn btn-primary btn-sm')
                .text('Gunakan kategori ini')
                .on('click', function () {
                    selectCategory(entry);
                });
            action.append(selectBtn);
            card.append(action);

            list.append(card);
        });

        panel.removeClass('d-none');
    }

    function selectCategory(entry) {
        if (!entry.kategori_user_id) {
            alertify.error('Kategori tidak valid');
            return;
        }

        $.ajax(withCsrf({
            type: 'POST',
            url: endpoints.setCategory,
            data: {
                nrp: $('#loginNrp').val().trim(),
                kategori_user_id: entry.kategori_user_id,
                comp_id: entry.comp_id,
                dept_id: entry.dept_id
            },
            success: function (result) {
                if (!result.status) {
                    alertify.error(result.remarks || 'Tidak dapat memilih kategori');
                    return;
                }

                if (!result.login_controller || !result.login_function) {
                    alertify.error('Menu belum dipetakan untuk kategori ini');
                    return;
                }

                window.location.href = '/' + result.login_controller + '/' + result.login_function;
            },
            error: function () {
                alertify.error('Gagal menghubungkan ke server');
            }
        }));
    }

    function initialsFromName(name) {
        if (!name) return '?';
        return name.trim().split(/\s+/).filter(Boolean).slice(0, 2).map(function (part) {
            return part.charAt(0).toUpperCase();
        }).join('') || '?';
    }

    function renderOnlineUsers(list) {
        var safeList = Array.isArray(list) ? list : [];
        $('#onlineUserCount').text(safeList.length + ' online');

        var group = $('#onlineUserList');
        if (!group.length) return;
        group.empty();
        if (!safeList.length) {
            group.append($('<span>').addClass('avatar-chip empty').text('Belum ada yang online'));
            return;
        }

        var maxChip = 6;
        safeList.slice(0, maxChip).forEach(function (name) {
            var chip = $('<span>').addClass('avatar-chip').attr('title', name || '');
            chip.text(initialsFromName(name));
            group.append(chip);
        });

        if (safeList.length > maxChip) {
            group.append($('<span>').addClass('avatar-chip more').text('+' + (safeList.length - maxChip)));
        }
    }

    function sendHeartbeat() {
        if (!endpoints.heartbeat) return;
        $.ajax(withCsrf({
            type: 'POST',
            url: endpoints.heartbeat,
            success: function (res) {
                if (res && res.status) {
                    renderOnlineUsers(res.data || []);
                }
            }
        }));
    }

    function submitLogin() {
        resetCategoryResult();
        var pnrp = $('#loginNrp').val().trim();
        var password = $('#loginPassword').val();
        var ip = $('#loginIp').val();

        if (!pnrp || !password) {
            alertify.error('NRP dan password tidak boleh kosong');
            return;
        }
        if (password.length < minPasswordLength) {
            alertify.error('Password minimal ' + minPasswordLength + ' karakter');
            return;
        }

        sendHeartbeat(); // mark online earlier
        $('#loginSubmitBtn').prop('disabled', true).text('Memproses...');
        $.ajax(withCsrf({
            type: 'POST',
            url: endpoints.login,
            data: {
                pnrp: pnrp,
                password: password,
                ip: ip
            },
            success: function (result) {
                if (!result.status) {
                    alertify.error(result.remarks || 'Login gagal');
                    $('#loginSubmitBtn').prop('disabled', false).text('Masuk Sekarang');
                    return;
                }

                alertify.success('Login berhasil, segera masuk ke dashboard');
                var effectiveNrp = result.user_nrp || pnrp;
                cacheNrp(effectiveNrp);
                $('#loginNrp').val(effectiveNrp);

                if (result.requiresPasswordReset) {
                    $('#firstLoginHiddenNrp').val(effectiveNrp);
                    $('#firstLoginNrp').val(effectiveNrp);
                    $('#firstLoginPasswordModal').modal('show');
                    $('#loginSubmitBtn').prop('disabled', false).text('Masuk Sekarang');
                    return;
                }

                var records = Array.isArray(result.data) ? result.data : [];
                renderCategoryResults(records, effectiveNrp);
                $('#loginSubmitBtn').prop('disabled', false).text('Masuk Sekarang');
            },
            error: function () {
                alertify.error('Gagal menghubungkan ke server');
                $('#loginSubmitBtn').prop('disabled', false).text('Masuk Sekarang');
            }
        }));
    }

    function submitSelfService() {
        var companyId = $('#selfServiceCompany').val();
        var noKtp = $('#selfServiceKtp').val().trim();
        var birthDate = $('#selfServiceBirth').val();
        var email = $('#selfServiceEmail').val().trim();
        if (!companyId || !noKtp || !birthDate || !email) {
            alertify.error('Lengkapi semua field pendaftaran');
            return;
        }

        var $btn = $('#selfServiceSubmit');
        $btn.prop('disabled', true).text('Mengirim...');

        $.ajax(withCsrf({
            type: 'POST',
            url: endpoints.selfServiceRegister,
            data: {
                companyId: companyId,
                noKtp: noKtp,
                tanggalLahir: birthDate,
                email: email
            },
            success: function (result) {
                $btn.prop('disabled', false).text('Daftar & Kirim Email');
                if (!result.status) {
                    alertify.error(result.remarks || 'Pendaftaran gagal');
                    return;
                }
                alertify.success(result.remarks || 'Akun berhasil dibuat, cek email');
                $('#selfServiceForm')[0].reset();
            },
            error: function () {
                $btn.prop('disabled', false).text('Daftar & Kirim Email');
                alertify.error('Gagal menghubungkan ke server');
            }
        }));
    }

    function setSelfServiceValidationMessage(text, severityClass) {
        var info = $('#selfServiceValidationInfo');
        if (!info.length) return;
        info.removeClass('text-muted text-danger text-success text-info d-none');
        if (!text) {
            info.addClass('d-none').text('');
            return;
        }
        info.addClass(severityClass || 'text-muted');
        info.text(text);
    }

    function setSelfServiceReady(isReady, message, severityClass) {
        var emailBlock = $('#selfServiceEmailBlock');
        var submitBlock = $('#selfServiceSubmitBlock');
        var submitBtn = $('#selfServiceSubmit');
        if (!emailBlock.length || !submitBlock.length || !submitBtn.length) {
            return;
        }
        emailBlock.toggleClass('d-none', !isReady);
        submitBlock.toggleClass('d-none', !isReady);
        submitBtn.prop('disabled', !isReady);
        setSelfServiceValidationMessage(message, severityClass);
    }

    function resetSelfServiceValidation() {
        setSelfServiceReady(false, 'Isi NIK dan tanggal lahir sesuai data master karyawan untuk membuka email dan tombol kirim.', 'text-muted');
    }

    var selfServiceValidationTimer;
    function scheduleSelfServiceValidation() {
        if (!endpoints.validateSelfService) {
            resetSelfServiceValidation();
            return;
        }

        clearTimeout(selfServiceValidationTimer);
        selfServiceValidationTimer = setTimeout(validateSelfServiceIdentity, 600);
    }

    function validateSelfServiceIdentity() {
        var noKtp = $('#selfServiceKtp').val();
        if (typeof noKtp === 'string') {
            noKtp = noKtp.trim();
        }
        var birthDate = $('#selfServiceBirth').val();
        if (!noKtp || !birthDate) {
            setSelfServiceReady(false, 'Lengkapi No. Identitas dan tanggal lahir untuk verifikasi.', 'text-muted');
            return;
        }

        setSelfServiceReady(false, 'Memverifikasi data dengan master karyawan...', 'text-info');

        $.ajax(withCsrf({
            type: 'POST',
            url: endpoints.validateSelfService,
            data: {
                noKtp: noKtp,
                tanggalLahir: birthDate
            },
            success: function (result) {
                if (result && result.status) {
                    setSelfServiceReady(true, result.remarks || 'Data terverifikasi', 'text-success');
                } else {
                    var message = result && result.remarks ? result.remarks : 'Data tidak cocok dengan master';
                    setSelfServiceReady(false, message, 'text-danger');
                }
            },
            error: function () {
                setSelfServiceReady(false, 'Tidak dapat menghubungkan server untuk verifikasi.', 'text-danger');
            }
        }));
    }

    function renderDeviceMeta() {
        var browserInfo = navigator.userAgent || 'Unknown browser';
        $('#deviceBrowser').text('Browser: ' + browserInfo);
        var ip = $('#loginIp').val() || '';
        if (ip) {
            $('#deviceIp').text('IP: ' + ip);
        }
        $('#deviceMac').text('MAC: tidak tersedia via browser untuk keamanan');
    }

    function attachEvents() {
        $('#loginForm').on('submit', function (event) {
            event.preventDefault();
            submitLogin();
        });

        $('#selfServiceForm').on('submit', function (event) {
            event.preventDefault();
            submitSelfService();
        });

        $('#selfServiceKtp, #selfServiceBirth').on('input change', function () {
            scheduleSelfServiceValidation();
        });

        var $selfServiceModal = $('#selfServiceModal');
        if ($selfServiceModal.length) {
            $selfServiceModal.on('shown.bs.modal', function () {
                resetSelfServiceValidation();
                setTimeout(function () {
                    $('#selfServiceKtp').focus();
                }, 200);
            });
            $selfServiceModal.on('hidden.bs.modal', function () {
                $('#selfServiceForm')[0].reset();
                resetSelfServiceValidation();
            });
        }

        $('#forgotCheck').on('click', function () {
            var identifier = $('#forgotIdentifier').val().trim();
            var birthDate = $('#forgotBirthDate').val();
            if (!identifier || !birthDate) {
                alertify.error('Lengkapi NRP/NIK dan tanggal lahir');
                return;
            }

            var $btn = $('#forgotCheck');
            $btn.prop('disabled', true).text('Mengirim OTP...');

            $.ajax(withCsrf({
                type: 'POST',
                url: endpoints.sendOtp,
                data: { identifier: identifier, birthDate: birthDate },
                success: function (result) {
                    $btn.prop('disabled', false).text('Verifikasi data');
                    if (!result.status) {
                        alertify.error(result.remarks || 'Data tidak ditemukan');
                        return;
                    }

                    forgotVerifiedNrp = result.user_nrp || identifier;
                    $('#forgotVerifiedInfo').text('Kode OTP dikirim ke email terdaftar. NRP: ' + (result.user_nrp || '-') + ' • Email: ' + (result.user_email || '-'));
                    $('#forgotResetSection').removeClass('d-none');
                    alertify.success(result.remarks || 'OTP terkirim');
                },
                error: function () {
                    $btn.prop('disabled', false).text('Verifikasi data');
                    alertify.error('Gagal mengirim OTP');
                }
            }));
        });

        $('#forgotReset').on('click', function () {
            var newPassword = $('#forgotNewPassword').val();
            var confirmPassword = $('#forgotConfirmPassword').val();
            var otp = $('#forgotOtp').val().trim();
            if (!forgotVerifiedNrp) {
                alertify.error('Silakan verifikasi data terlebih dahulu');
                return;
            }
            if (!otp) {
                alertify.error('Masukkan kode OTP');
                return;
            }
            if (!newPassword || !confirmPassword) {
                alertify.error('Password baru dan konfirmasi harus diisi');
                return;
            }
            if (newPassword.length < minPasswordLength) {
                alertify.error('Password minimal ' + minPasswordLength + ' karakter');
                return;
            }
            if (newPassword !== confirmPassword) {
                alertify.error('Konfirmasi password tidak sesuai');
                return;
            }

            var $btn = $('#forgotReset');
            $btn.prop('disabled', true).text('Memproses...');

            $.ajax(withCsrf({
                type: 'POST',
                url: endpoints.resetWithOtp,
                data: { identifier: forgotVerifiedNrp, otp: otp, newPassword: newPassword },
                success: function (result) {
                    $btn.prop('disabled', false).text('Reset password');
                    if (!result.status) {
                        alertify.error(result.remarks || 'Reset gagal');
                        return;
                    }
                    alertify.success('Password berhasil diperbarui');
                    $('#forgotPasswordModal').modal('hide');
                    $('#forgotIdentifier').val('');
                    $('#forgotBirthDate').val('');
                    $('#forgotOtp').val('');
                    $('#forgotNewPassword').val('');
                    $('#forgotConfirmPassword').val('');
                    $('#forgotResetSection').addClass('d-none');
                    forgotVerifiedNrp = '';
                },
                error: function () {
                    $btn.prop('disabled', false).text('Reset password');
                    alertify.error('Gagal menghubungkan ke server');
                }
            }));
        });

        $('#firstLoginSave').on('click', function () {
            var nrp = $('#firstLoginHiddenNrp').val();
            var newPassword = $('#firstLoginNewPassword').val();
            var confirmPassword = $('#firstLoginConfirmPassword').val();
            if (!newPassword || !confirmPassword) {
                alertify.error('Lengkapi password baru dan konfirmasi');
                return;
            }
            if (newPassword.length < minPasswordLength) {
                alertify.error('Password minimal ' + minPasswordLength + ' karakter');
                return;
            }
            if (newPassword !== confirmPassword) {
                alertify.error('Konfirmasi password tidak cocok');
                return;
            }

            $.ajax(withCsrf({
                type: 'POST',
                url: endpoints.resetWithoutOtp,
                data: { nrp: nrp, newPassword: newPassword },
                success: function (result) {
                    if (!result.status) {
                        alertify.error(result.remarks || 'Gagal memperbarui password');
                        return;
                    }
                    alertify.success('Password baru berhasil diterapkan');
                    $('#firstLoginPasswordModal').modal('hide');
                    setTimeout(function () {
                        window.location.reload();
                    }, 400);
                },
                error: function () {
                    alertify.error('Gagal menghubungkan ke server');
                }
            }));
        });
    }

    $(function () {
        resetCategoryResult();
        attachEvents();
        sendHeartbeat();
        setInterval(sendHeartbeat, onlineIntervalMs);
        renderDeviceMeta();
        loadCachedNrp();
        if ($('#selfServiceModal').length) {
            resetSelfServiceValidation();
        }
    });
})(jQuery);
