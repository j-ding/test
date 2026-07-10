document.addEventListener('DOMContentLoaded', () => {

    // ── Copy buttons ────────────────────────────────────────────────────────
    document.querySelectorAll('.copy-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const ta = document.getElementById(btn.dataset.target);
            navigator.clipboard.writeText(ta.value).then(() => {
                const orig = btn.textContent;
                btn.textContent = 'Copied!';
                btn.classList.replace('btn-outline-secondary', 'btn-success');
                setTimeout(() => {
                    btn.textContent = orig;
                    btn.classList.replace('btn-success', 'btn-outline-secondary');
                }, 2000);
            });
        });
    });

    // ── Hide / Show toggle text via Bootstrap collapse events ───────────────
    document.querySelectorAll('.collapse').forEach(el => {
        el.addEventListener('show.bs.collapse', () => {
            const btn = document.querySelector(`[data-bs-target="#${el.id}"]`);
            if (btn) btn.textContent = 'Hide';
        });
        el.addEventListener('hide.bs.collapse', () => {
            const btn = document.querySelector(`[data-bs-target="#${el.id}"]`);
            if (btn) btn.textContent = 'Show';
        });
    });

});

// ── Inline email editing ─────────────────────────────────────────────────────
// Originals are stashed as JS properties on the DOM elements to avoid
// HTML-encoding issues with multiline text in data attributes.

function enterEditMode(id) {
    const ta = document.getElementById(`ta-${id}`);
    const input = document.getElementById(`subject-input-${id}`);
    const subjectStrong = document.querySelector(`#subject-view-${id} strong`);
    const recipientsInput = document.getElementById(`recipients-input-${id}`);

    ta._original = ta.value;
    input._original = subjectStrong.textContent;
    recipientsInput._original = recipientsInput.value;

    document.getElementById(`subject-view-${id}`).classList.add('d-none');
    document.getElementById(`subject-edit-${id}`).classList.remove('d-none');
    document.getElementById(`recipients-view-${id}`).classList.add('d-none');
    document.getElementById(`recipients-edit-${id}`).classList.remove('d-none');
    document.getElementById(`view-actions-${id}`).classList.add('d-none');
    document.getElementById(`edit-actions-${id}`).classList.remove('d-none');

    ta.removeAttribute('readonly');
    ta.classList.add('border-warning', 'bg-white');
    ta.focus();
}

function cancelEditMode(id) {
    const ta = document.getElementById(`ta-${id}`);
    const input = document.getElementById(`subject-input-${id}`);
    const recipientsInput = document.getElementById(`recipients-input-${id}`);

    ta.value = ta._original;
    input.value = input._original;
    recipientsInput.value = recipientsInput._original;

    document.getElementById(`subject-view-${id}`).classList.remove('d-none');
    document.getElementById(`subject-edit-${id}`).classList.add('d-none');
    document.getElementById(`recipients-view-${id}`).classList.remove('d-none');
    document.getElementById(`recipients-edit-${id}`).classList.add('d-none');
    document.getElementById(`view-actions-${id}`).classList.remove('d-none');
    document.getElementById(`edit-actions-${id}`).classList.add('d-none');

    ta.setAttribute('readonly', '');
    ta.classList.remove('border-warning', 'bg-white');
}

async function sendEmail(id) {
    const btn = document.getElementById(`send-btn-${id}`);
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    const origText = btn.textContent;
    btn.textContent = 'Sending…';
    btn.disabled = true;

    try {
        const resp = await fetch(`/Incidents/SendEmail/${id}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({ __RequestVerificationToken: token })
        });

        if (resp.ok) {
            btn.textContent = 'Sent!';
            btn.classList.replace('btn-outline-success', 'btn-success');
            setTimeout(() => {
                btn.textContent = origText;
                btn.classList.replace('btn-success', 'btn-outline-success');
                btn.disabled = false;
            }, 3000);
        } else {
            const data = await resp.json().catch(() => ({}));
            alert(`Send failed: ${data.error || 'Unknown error'}`);
            btn.textContent = origText;
            btn.disabled = false;
        }
    } catch {
        alert('Network error — please try again.');
        btn.textContent = origText;
        btn.disabled = false;
    }
}

async function saveEmail(id) {
    const subject = document.getElementById(`subject-input-${id}`).value.trim();
    const body = document.getElementById(`ta-${id}`).value;
    const recipients = document.getElementById(`recipients-input-${id}`).value.trim();
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    const saveBtn = document.getElementById(`save-btn-${id}`);
    saveBtn.textContent = 'Saving…';
    saveBtn.disabled = true;

    try {
        const resp = await fetch(`/Incidents/EditEmail/${id}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({ subject, body, recipients, __RequestVerificationToken: token })
        });

        if (resp.ok) {
            document.querySelector(`#subject-view-${id} strong`).textContent = subject;
            document.querySelector(`#recipients-view-${id} strong`).textContent = recipients || '(site default — see appsettings.json)';
            const ta = document.getElementById(`ta-${id}`);
            const input = document.getElementById(`subject-input-${id}`);
            const recipientsInput = document.getElementById(`recipients-input-${id}`);
            ta._original = body;
            input._original = subject;
            recipientsInput._original = recipients;
            cancelEditMode(id);
        } else {
            alert('Save failed — please try again.');
        }
    } catch {
        alert('Network error — please try again.');
    } finally {
        saveBtn.textContent = 'Save';
        saveBtn.disabled = false;
    }
}
