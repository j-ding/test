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

    // ── Recipients directory picker ──────────────────────────────────────────
    document.querySelectorAll('.recipients-picker').forEach(el => {
        initRecipientsPicker(el.dataset.emailId);
    });

});

// ── Recipients directory picker ──────────────────────────────────────────────
// One picker per email card. Selected people are kept both as an in-memory list
// (for the chip UI) and mirrored into the hidden recipients-input as a
// comma-separated string, which is what actually gets submitted/saved.
window.__recipientPickers = {};

function initRecipientsPicker(id) {
    const hidden = document.getElementById(`recipients-input-${id}`);
    const chipsEl = document.getElementById(`recipients-chips-${id}`);
    const searchEl = document.getElementById(`recipients-search-${id}`);
    const dropdownEl = document.getElementById(`recipients-dropdown-${id}`);
    if (!hidden || !chipsEl || !searchEl || !dropdownEl) return;

    let selected = [];

    function setFromString(value) {
        selected = (value || '').split(',')
            .map(s => s.trim())
            .filter(Boolean)
            .map(email => ({ email, displayName: email }));
        renderChips();
    }

    function renderChips() {
        chipsEl.innerHTML = '';
        selected.forEach((person, idx) => {
            const chip = document.createElement('span');
            chip.className = 'badge rounded-pill text-bg-light border d-inline-flex align-items-center gap-1';

            const label = document.createElement('span');
            label.textContent = person.displayName || person.email;
            chip.appendChild(label);

            const removeBtn = document.createElement('button');
            removeBtn.type = 'button';
            removeBtn.className = 'btn-close';
            removeBtn.style.fontSize = '0.55rem';
            removeBtn.setAttribute('aria-label', 'Remove');
            removeBtn.addEventListener('click', () => {
                selected.splice(idx, 1);
                syncAndRender();
            });
            chip.appendChild(removeBtn);

            chipsEl.appendChild(chip);
        });
    }

    function syncAndRender() {
        hidden.value = selected.map(p => p.email).join(', ');
        renderChips();
    }

    function renderDropdown(results) {
        dropdownEl.innerHTML = '';
        const filtered = results.filter(r => !selected.some(s => s.email.toLowerCase() === r.email.toLowerCase()));
        if (filtered.length === 0) {
            dropdownEl.classList.add('d-none');
            return;
        }
        filtered.forEach(person => {
            const item = document.createElement('button');
            item.type = 'button';
            item.className = 'list-group-item list-group-item-action py-1 px-2 small text-start';

            const nameEl = document.createElement('div');
            nameEl.className = 'fw-semibold';
            nameEl.textContent = person.displayName;

            const emailEl = document.createElement('div');
            emailEl.className = 'text-muted';
            emailEl.textContent = person.email;

            item.appendChild(nameEl);
            item.appendChild(emailEl);
            item.addEventListener('click', () => {
                selected.push(person);
                syncAndRender();
                searchEl.value = '';
                dropdownEl.classList.add('d-none');
                searchEl.focus();
            });
            dropdownEl.appendChild(item);
        });
        dropdownEl.classList.remove('d-none');
    }

    let debounceTimer;
    searchEl.addEventListener('input', () => {
        clearTimeout(debounceTimer);
        const q = searchEl.value.trim();
        if (q.length < 2) {
            dropdownEl.classList.add('d-none');
            return;
        }
        debounceTimer = setTimeout(async () => {
            try {
                const resp = await fetch(`/Incidents/SearchRecipients?q=${encodeURIComponent(q)}`);
                if (!resp.ok) return;
                renderDropdown(await resp.json());
            } catch {
                // transient search error — leave dropdown as-is, user can retype
            }
        }, 250);
    });

    document.addEventListener('click', (e) => {
        if (e.target !== searchEl && !dropdownEl.contains(e.target)) {
            dropdownEl.classList.add('d-none');
        }
    });

    setFromString(hidden.value);
    window.__recipientPickers[id] = { setFromString };
}

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
    window.__recipientPickers[id]?.setFromString(recipientsInput.value);

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
    window.__recipientPickers[id]?.setFromString(recipientsInput.value);

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
