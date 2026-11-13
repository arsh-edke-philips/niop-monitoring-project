document.addEventListener('DOMContentLoaded', function () {
    const modal = document.getElementById('locationModal');
    if (!modal) return;

    const title = document.getElementById('locationModalTitle');
    const centerName = document.getElementById('locationModalCenterName');
    const sections = modal.querySelectorAll('.center-locations');
    const empty = modal.querySelector('.center-empty');

    const openModal = () => { modal.hidden = false; };
    const closeModal = () => { modal.hidden = true; };

    document.querySelectorAll('.dc-device-card.clickable').forEach(card => {
        card.addEventListener('click', () => {
            const centerId = card.dataset.centerId;
            const name = card.dataset.centerName;
            title.textContent = 'Devices by location';
            centerName.textContent = name;

            let found = false;
            sections.forEach(s => {
                const match = s.dataset.centerId === centerId;
                s.hidden = !match;
                if (match) found = true;
            });
            empty.hidden = found;

            openModal();
        });
    });

    modal.querySelector('.modal-close').addEventListener('click', closeModal);
    modal.addEventListener('click', e => { if (e.target === modal) closeModal(); });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') closeModal(); });
});