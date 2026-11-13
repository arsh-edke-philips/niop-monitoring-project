(() => {
    const DASHBOARD_URL = "/";

    // entity name from the catalog -> the catalog column holding its number -> the list id in Index.cshtml
    const LINEAGE = {
        device:       { key: "SerialNumber",     list: "device-list" },
        order:        { key: "OrderNumber",      list: "order-list" },
        enrollment:   { key: "EnrollmentNumber", list: "enrollment-list" },
        report:       { key: "ReportNumber",     list: "report-list" },
        prebuilt_kit: { key: "PBKSerialNumber",  list: "pbk-list" },
        transfer:     { key: "TransferNumber",   list: "transfer-list" },
    };

    const $ = id => document.getElementById(id);
    const panel = $("chat-panel");
    const messages = $("chat-messages");
    const form = $("chat-form");
    const input = $("chat-input");
    const sendButton = $("chat-send");
    const token = () => form.querySelector("input[name='__RequestVerificationToken']").value;

    $("chat-toggle").addEventListener("click", () => {
        panel.hidden = !panel.hidden;
        if (!panel.hidden) input.focus();
    });
    $("chat-close").addEventListener("click", () => { panel.hidden = true; });

    $("chat-new").addEventListener("click", async () => {
        await fetch("/chat/new", { method: "POST", headers: { RequestVerificationToken: token() } });
        messages.replaceChildren();
        input.focus();
    });

    input.addEventListener("keydown", e => {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            form.requestSubmit();
        }
    });

    form.addEventListener("submit", async e => {
        e.preventDefault();
        const text = input.value.trim();
        if (!text || sendButton.disabled) return;
        input.value = "";
        addBubble("user", text);
        await ask(text);
    });

    async function ask(text) {
        sendButton.disabled = true;
        const status = addBubble("status", "Thinking...");
        const answer = addBubble("bot", "");
        const tables = document.createElement("div");
        answer.after(tables);

        try {
            const resp = await fetch("/chat/send", {
                method: "POST",
                headers: { "Content-Type": "application/json", RequestVerificationToken: token() },
                body: JSON.stringify({ message: text }),
            });
            if (!resp.ok) {
                answer.textContent = errorText(resp.status);
                return;
            }
            await readEvents(resp, (kind, data) => {
                if (kind === "tool") status.textContent = describeTool(data);
                else if (kind === "table") tables.appendChild(renderTable(data));
                else if (kind === "delta") answer.textContent += data.text;
                else if (kind === "error") answer.textContent = data.message;
                scrollDown();
            });
            if (!answer.textContent) answer.textContent = "(no answer)";
        } catch (error) {
            console.error("Chat request failed:", error);
            answer.textContent = "Connection lost. Please try again.";
        } finally {
            status.remove();
            sendButton.disabled = false;
            input.focus();
        }
    }

    async function readEvents(resp, onEvent) {
        const reader = resp.body.getReader();
        const decoder = new TextDecoder();
        let buffer = "";
        for (;;) {
            const { value, done } = await reader.read();
            if (done) return;
            buffer += decoder.decode(value, { stream: true });

            let end;
            while ((end = buffer.indexOf("\n\n")) >= 0) {
                const block = buffer.slice(0, end);
                buffer = buffer.slice(end + 2);
                let kind = "message", data = "";
                for (const line of block.split("\n")) {
                    if (line.startsWith("event: ")) kind = line.slice(7);
                    else if (line.startsWith("data: ")) data = line.slice(6);
                }
                if (data) onEvent(kind, JSON.parse(data));
            }
        }
    }

    function renderTable({ entity, rows, truncated }) {
        const wrap = document.createElement("div");
        wrap.className = "chat-table";
        const columns = Object.keys(rows[0]);
        const lineage = LINEAGE[entity];

        const table = document.createElement("table");
        const head = table.createTHead().insertRow();
        for (const c of columns) {
            const th = document.createElement("th");
            th.textContent = c;
            head.appendChild(th);
        }
        const body = table.createTBody();
        for (const row of rows) {
            const tr = body.insertRow();
            for (const c of columns) tr.insertCell().textContent = row[c] ?? "";
            if (lineage) {
                const cell = tr.insertCell();
                const value = row[lineage.key];
                if (value != null) cell.appendChild(lineageButton(entity, value));
            }
        }

        wrap.appendChild(table);
        if (truncated) {
            const note = document.createElement("p");
            note.className = "chat-note";
            note.textContent = "Showing the first rows only. More exist.";
            wrap.appendChild(note);
        }
        return wrap;
    }

        function findLineageRow(entity, value) {
        const list = document.getElementById(LINEAGE[entity].list);
        if (!list) return null;
        const wanted = String(value).trim().toLowerCase();
        for (const row of list.querySelectorAll(".record-row")) {
            const label = (row.querySelector("span")?.textContent ?? "")
                .trim().toLowerCase().replace(/^transfer #/, "");
            if (label === wanted) return row;
        }
        return null;
    }

    function openLineage(entity, value) {
        const row = findLineageRow(entity, value);
        if (!row) return false;
        row.click();
        return true;
    }

    function lineageButton(entity, value) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "chat-lineage";
        button.textContent = "View lineage";
        button.addEventListener("click", () => {
            const onDashboard = document.getElementById(LINEAGE[entity].list) !== null;
            if (!onDashboard) {
                const url = `${DASHBOARD_URL}?lineage=${encodeURIComponent(entity)}&id=${encodeURIComponent(value)}`;
                window.open(url, "_blank", "noopener");
                return;
            }
            if (!openLineage(entity, value)) {
                button.disabled = true;
                button.textContent = "Not in dashboard list";
            }
        });
        return button;
    }

    function describeTool({ name, arguments: args = {} }) {
        const what = (args.entity || "records").replaceAll("_", " ");
        if (name === "find_by_identifier") return `Looking up ${args.value}...`;
        if (name === "count_entity") return `Counting ${what}...`;
        return `Searching ${what}...`;
    }

    function errorText(status) {
        if (status === 409) return "Still answering your previous question. Please wait.";
        if (status === 400) return "The message is empty or too long.";
        if (status === 503) return "The assistant is not available right now.";
        return "Something went wrong. Please try again.";
    }

    function addBubble(role, text) {
        const div = document.createElement("div");
        div.className = `chat-msg chat-${role}`;
        div.textContent = text;
        messages.appendChild(div);
        scrollDown();
        return div;
    }

    function scrollDown() {
        messages.scrollTop = messages.scrollHeight;
    }

    const params = new URLSearchParams(location.search);
    const linkEntity = params.get("lineage");
    const linkId = params.get("id");
    if (linkEntity && linkId && Object.hasOwn(LINEAGE, linkEntity)) {
        window.addEventListener("load", () => {
            if (!openLineage(linkEntity, linkId)) {
                panel.hidden = false;
                addBubble("status", `${linkEntity.replaceAll("_", " ")} ${linkId} is not in the dashboard list.`);
            }
        });
    }
})();