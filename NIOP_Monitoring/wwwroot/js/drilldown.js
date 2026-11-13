document.addEventListener('DOMContentLoaded', function () {
    // Device modal - populate from data attribute
    const deviceModal = document.getElementById('deviceTreeModal');
    if (deviceModal) {
        deviceModal.addEventListener('show.bs.modal', function (e) {
            const button = e.relatedTarget;
            const deviceData = JSON.parse(button.dataset.deviceData);

            document.getElementById('deviceHeader').textContent = `Device: ${deviceData.device}`;
            document.getElementById('dev-id').textContent = deviceData.device || '—';
            document.getElementById('dev-type').textContent = deviceData.deviceType || '—';
            document.getElementById('dev-category').textContent = deviceData.deviceCategory || '—';
            document.getElementById('dev-config').textContent = deviceData.deviceConfig || '—';
            document.getElementById('dev-part').textContent = deviceData.part || '—';
            document.getElementById('dev-location').textContent = deviceData.location || '—';
            document.getElementById('dev-center').textContent = deviceData.center || '—';
            document.getElementById('dev-facility').textContent = deviceData.facility || '—';
            document.getElementById('dev-po').textContent = deviceData.purchaseOrderNumber || '—';
            document.getElementById('dev-bundle').textContent = deviceData.bundle || '—';
            document.getElementById('dev-order').textContent = deviceData.order || '—';
        });
    }

    // Order modal - populate from data attribute
    const orderModal = document.getElementById('orderTreeModal');
    if (orderModal) {
        orderModal.addEventListener('show.bs.modal', function (e) {
            const button = e.relatedTarget;
            const orderData = JSON.parse(button.dataset.orderData);

            document.getElementById('orderHeader').textContent = `Order: ${orderData.orderNumber}`;
            document.getElementById('ord-id').textContent = orderData.orderNumber || '—';
            document.getElementById('ord-type').textContent = orderData.orderType || '—';
            document.getElementById('ord-status').textContent = orderData.orderStatus || '—';
            document.getElementById('ord-center').textContent = orderData.center || '—';
            document.getElementById('ord-facility').textContent = orderData.facility || '—';
            document.getElementById('ord-user').textContent = orderData.createdUser || '—';
            document.getElementById('ord-kit').textContent = orderData.kit || '—';
            document.getElementById('ord-prebuilt').textContent = orderData.preBuiltKit || '—';
            document.getElementById('ord-bundle').textContent = orderData.bundle || '—';
            document.getElementById('ord-device').textContent = orderData.device || '—';
        });
    }
});


document.addEventListener('DOMContentLoaded', function () {
    const searchInputs = document.querySelectorAll('.lineage-search');

    searchInputs.forEach(function (input) {
        input.addEventListener('keyup', function () {
            const query = this.value.toLowerCase();
            const targetId = this.dataset.target;
            const rows = document.querySelectorAll('#' + targetId + ' .record-row');

            rows.forEach(function (row) {
                row.style.display = row.textContent.toLowerCase().indexOf(query) !== -1 ? '' : 'none';
            });
        });
    });

    const getData = function (button, attr) {
        const raw = button.getAttribute(attr);
        return raw ? JSON.parse(raw) : {};
    };

    const enrollmentModal = document.getElementById('enrollmentTreeModal');
    if (enrollmentModal) {
        enrollmentModal.addEventListener('show.bs.modal', function (e) {
            const data = getData(e.relatedTarget, 'data-enrollment-data');
            const attachment = (data.attachment && data.attachment[0]) || {};
            const report = data.report || {};
            document.getElementById('enrollmentHeader').textContent = 'Enrollment: ' + (data.enrollmentNumber || '—');
            document.getElementById('enr-status').textContent = data.enrollmentStatus || '—';
            document.getElementById('enr-deviceType').textContent = data.deviceType || '—';
            document.getElementById('enr-billingMethod').textContent = data.billingMethod || '—';
            document.getElementById('enr-billingStatus').textContent = data.billingStatus || '—';
            document.getElementById('enr-pendingAttachments').textContent = data.pendingAttachmentCount ?? '—';
            document.getElementById('enr-facility').textContent = data.facility || '—';
            document.getElementById('enr-attachmentId').textContent = attachment.attachmentId || '—';
            document.getElementById('enr-contentType').textContent = attachment.contentType || '—';
            document.getElementById('enr-attachmentType').textContent = attachment.attachmentType || '—';
            document.getElementById('enr-objectType').textContent = data.objectType || '—';
            document.getElementById('enr-reportNumber').textContent = report.reportNumber || '—';
            document.getElementById('enr-reportType').textContent = report.reportType || '—';
            document.getElementById('enr-reportFileId').textContent = report.reportFileId || '—';
            document.getElementById('enr-orderNumber').textContent = data.orderNumber || '—';
        });
    }

    const reportModal = document.getElementById('reportTreeModal');
    if (reportModal) {
        reportModal.addEventListener('show.bs.modal', function (e) {
            const data = getData(e.relatedTarget, 'data-report-data');
            const attachment = data.attachment || {};
            const enrollment = data.enrollment || {};
            document.getElementById('reportHeader').textContent = 'Report: ' + (data.reportNumber ?? '');
            document.getElementById('rep-status').textContent = data.reportStatus || '—';
            document.getElementById('rep-type').textContent = data.reportType || '—';
            document.getElementById('rep-number').textContent = data.reportNumber || '—';
            document.getElementById('rep-onhold').textContent = data.onHold ?? '—';
            document.getElementById('rep-onholdby').textContent = data.onHoldBy || '—';
            document.getElementById('rep-onholdreason').textContent = data.onHoldReason || '—';
            document.getElementById('rep-attachmentId').textContent = attachment.attachmentId || '—';
            document.getElementById('rep-attachmentType').textContent = attachment.attachmentType || '—';
            document.getElementById('rep-objectType').textContent = attachment.objectType || data.objectType || '—';
            document.getElementById('rep-contentType').textContent = attachment.contentType || '—';
            document.getElementById('rep-enrollmentNumber').textContent = enrollment.enrollmentNumber || '—';
            document.getElementById('rep-enrollmentStatus').textContent = enrollment.enrollmentStatus || '—';
            document.getElementById('rep-enrollmentDeviceType').textContent = enrollment.deviceType || '—';
            document.getElementById('rep-enrollmentFacility').textContent = enrollment.facility || '—';
        });
    }

    const pbkModal = document.getElementById('pbkTreeModal');
    if (pbkModal) {
        pbkModal.addEventListener('show.bs.modal', function (e) {
            const data = getData(e.relatedTarget, 'data-pbk-data');
            const partNumbers = Array.isArray(data.partNumbers)
                ? data.partNumbers.filter(p => p)
                : (typeof data.partNumbers === 'string' && data.partNumbers.trim() ? [data.partNumbers] : []);

            document.getElementById('pbkHeader').textContent = 'PBK: ' + (data.pbkSerialNumber || '');
            document.getElementById('pbk-number').textContent = data.pbkSerialNumber || '—';
            document.getElementById('pbk-status').textContent = data.pbkStatus || '—';
            document.getElementById('pbk-center').textContent = data.center || '—';
            document.getElementById('pbk-bundle').textContent = data.bundleNumber || '—';
            document.getElementById('pbk-expiration').textContent = data.expirationDate || '—';
            const pbkPartNumberElement = document.getElementById('pbk-partNumber');
            pbkPartNumberElement.replaceChildren();
            if (partNumbers.length === 0) {
                pbkPartNumberElement.textContent = '—';
            } else {
                partNumbers.forEach(partNumber => {
                    const line = document.createElement('span');
                    line.textContent = partNumber;
                    line.style.display = 'block';
                    pbkPartNumberElement.appendChild(line);
                });
            }
            document.getElementById('pbk-kit').textContent = data.kit || '—';
            document.getElementById('pbk-orderNumber').textContent = data.orderNumber || '—';
            document.getElementById('pbk-orderStatus').textContent = data.orderStatus || '—';
            document.getElementById('pbk-orderType').textContent = data.orderType || '—';
            document.getElementById('pbk-orderCenter').textContent = data.orderCenter || '—';
        });
    }

    const transferModal = document.getElementById('transferTreeModal');
    if (transferModal) {
        transferModal.addEventListener('show.bs.modal', function (e) {
            const data = getData(e.relatedTarget, 'data-transfer-data');
            document.getElementById('transferHeader').textContent = 'Transfer: ' + (data.transferNumber || '—');
            document.getElementById('trn-number').textContent = data.transferNumber || '—';
            document.getElementById('trn-status').textContent = data.transferStatus || '—';
            document.getElementById('trn-note').textContent = data.note || '—';
            document.getElementById('trn-centerFrom').textContent = data.fromCenter || '—';
            document.getElementById('trn-centerTo').textContent = data.toCenter || '—';

            const transferDevices = Array.isArray(data.device) ? data.device   : [];
            const transferDevicesElement = document.getElementById('trn-devices');
            transferDevicesElement.replaceChildren();

            if (transferDevices.length === 0) {
                transferDevicesElement.textContent = '—';
            } else {
                transferDevices.forEach(device => {
                    const line = document.createElement('span');
                    const serial = device.device || '—';
                    const type = device.deviceType || '';
                    line.textContent = type ? `${serial} (${type})` : serial;
                    line.style.display = 'block';
                    transferDevicesElement.appendChild(line);
                });
            }
        });
    }
});
