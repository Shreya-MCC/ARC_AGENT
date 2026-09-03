const APP_NAME = "ARC Assistant";

let sessionId = null;

function apiHeaders(json = false) {
  const headers = {
    "X-Arc-Upn": "chat@local.dev",
    "X-Arc-Role": "Legal",
  };
  if (json) headers["Content-Type"] = "application/json";
  return headers;
}

const chatThread = document.getElementById("chatThread");
const chatBody = document.getElementById("chatBody");
const chatForm = document.getElementById("chatForm");
const messageInput = document.getElementById("messageInput");
const sendBtn = document.getElementById("sendBtn");
const sessionSummary = document.getElementById("sessionSummary");
const resetSessionBtn = document.getElementById("resetSession");
const quickReplies = document.getElementById("quickReplies");
const headerStatus = document.getElementById("headerStatus");
const sidebar = document.getElementById("sidebar");
const sidebarToggle = document.getElementById("sidebarToggle");
const sidebarBackdrop = document.getElementById("sidebarBackdrop");

const QUICK_PROMPTS = [
  { label: "Depot 006", text: "006" },
  { label: "WB", text: "WB" },
  { label: "WB table", text: "WB depots in table format" },
  { label: "E1-WB grid", text: "E1-WB in grid format" },
];

function escapeHtml(text) {
  const el = document.createElement("span");
  el.textContent = text;
  return el.innerHTML;
}

function formatTime(date) {
  return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

function renderMarkdownLite(text) {
  return escapeHtml(text)
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/\*([^*]+)\*/g, "<em>$1</em>")
    .replace(/\n/g, "<br>");
}

function appendMessage(text, type, agent = APP_NAME, replyFormat = "text", at = new Date()) {
  const wrap = document.createElement("div");
  wrap.className = `message ${type}`;

  const bubble = document.createElement("div");
  bubble.className = "bubble";

  if (replyFormat === "html") {
    const rich = document.createElement("div");
    rich.className = "rich-reply";
    rich.innerHTML = text;
    bubble.appendChild(rich);
  } else {
    const p = document.createElement("p");
    p.innerHTML = renderMarkdownLite(text);
    bubble.appendChild(p);
  }

  const meta = document.createElement("span");
  meta.className = "meta";
  meta.textContent = type === "sent" ? formatTime(at) : `${agent} · ${formatTime(at)}`;
  bubble.appendChild(meta);

  wrap.appendChild(bubble);
  chatThread.appendChild(wrap);
  chatBody.scrollTop = chatBody.scrollHeight;
  return wrap;
}

function showTyping() {
  const wrap = document.createElement("div");
  wrap.className = "message received";
  wrap.id = "typingIndicator";

  const bubble = document.createElement("div");
  bubble.className = "bubble typing";
  bubble.innerHTML = '<span class="typing-dot"></span><span class="typing-dot"></span><span class="typing-dot"></span>';
  wrap.appendChild(bubble);
  chatThread.appendChild(wrap);
  chatBody.scrollTop = chatBody.scrollHeight;
}

function hideTyping() {
  document.getElementById("typingIndicator")?.remove();
}

function showQuickReplies() {
  quickReplies.innerHTML = "";
  QUICK_PROMPTS.forEach((item) => {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "chip";
    btn.textContent = item.label;
    btn.addEventListener("click", () => handleSubmit(item.text));
    quickReplies.appendChild(btn);
  });
  quickReplies.hidden = false;
}

function setOnlineStatus(note = "") {
  const suffix = note ? ` · ${note}` : "";
  headerStatus.innerHTML = `<span class="status-dot"></span>Online · ARC Assistant${suffix}`;
}

function updateSessionSummary() {
  const label = sessionId ? `${sessionId.slice(0, 8)}…` : "loading…";
  sessionSummary.innerHTML = `
    <div><dt>Session</dt><dd title="${escapeHtml(sessionId || "")}">${escapeHtml(label)}</dd></div>
    <div><dt>Mode</dt><dd>Depot lookup</dd></div>
    <div><dt>Source</dt><dd>depot_mstr</dd></div>
    <div><dt>History</dt><dd>Cosmos · knowledgeChunks</dd></div>
  `;
}

function closeSidebar() {
  sidebar.classList.remove("open");
  sidebarBackdrop.hidden = true;
}

function toggleSidebar() {
  const open = sidebar.classList.toggle("open");
  sidebarBackdrop.hidden = !open;
}

function showWelcome() {
  setOnlineStatus();
  messageInput.placeholder = "Type a depot code, name, or region…";
  showQuickReplies();

  showTyping();
  setTimeout(() => {
    hideTyping();
    appendMessage(
      "Hello — I'm **ARC Assistant**.\n\n"
      + "I help you look up depot master data — codes, names, regions, and states. "
      + "You can ask using shortcuts like **WB** for West Bengal, or request results **in table or grid format**.",
      "received"
    );
  }, 700);
}

function renderHistory(messages) {
  messages.forEach((item) => {
    const type = item.role === "user" ? "sent" : "received";
    const at = item.timestamp ? new Date(item.timestamp) : new Date();
    appendMessage(
      item.content,
      type,
      item.agent || APP_NAME,
      item.replyFormat || "text",
      at
    );
  });
  setOnlineStatus("history restored from Cosmos");
  showQuickReplies();
}

async function loadCurrentSession() {
  try {
    const response = await fetch("/v1/chat/sessions/current", { headers: apiHeaders() });
    if (!response.ok) return false;
    const data = await response.json();
    sessionId = data.sessionId || null;
    updateSessionSummary();
    const messages = data.messages || [];
    if (messages.length === 0) return false;
    renderHistory(messages);
    return true;
  } catch {
    return false;
  }
}

async function sendApiMessage(text) {
  showTyping();
  sendBtn.disabled = true;

  try {
    const response = await fetch("/v1/chat/messages", {
      method: "POST",
      headers: apiHeaders(true),
      body: JSON.stringify({ message: text, sessionId }),
    });

    hideTyping();

    if (!response.ok) {
      const err = await response.json().catch(() => ({}));
      appendMessage(err.error || `Request failed (${response.status})`, "received");
      return;
    }

    const data = await response.json();
    if (data.sessionId) {
      sessionId = data.sessionId;
      updateSessionSummary();
    }
    appendMessage(data.reply, "received", data.agent || APP_NAME, data.replyFormat || "text");
    if (data.historySaved === false) {
      setOnlineStatus("history not saved");
      if (data.historyError) {
        appendMessage(`Chat history was not saved to Cosmos.\n\n${data.historyError}`, "received", "System");
      }
    } else {
      setOnlineStatus("session saved to Cosmos");
    }
  } catch (error) {
    hideTyping();
    appendMessage(`Network error: ${error.message}`, "received");
  } finally {
    sendBtn.disabled = false;
    messageInput.focus();
  }
}

function handleSubmit(text) {
  if (!text) return;
  appendMessage(text, "sent");
  sendApiMessage(text);
}

async function resetChat() {
  try {
    const response = await fetch("/v1/chat/sessions", {
      method: "POST",
      headers: apiHeaders(true),
    });
    if (response.ok) {
      const data = await response.json();
      sessionId = data.sessionId || null;
    } else {
      sessionId = null;
    }
  } catch {
    sessionId = null;
  }

  chatThread.innerHTML = "";
  closeSidebar();
  updateSessionSummary();
  showWelcome();
}

sidebarToggle.addEventListener("click", toggleSidebar);
sidebarBackdrop.addEventListener("click", closeSidebar);
resetSessionBtn.addEventListener("click", resetChat);

chatForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const text = messageInput.value.trim();
  if (!text) return;
  messageInput.value = "";
  handleSubmit(text);
});

updateSessionSummary();
messageInput.placeholder = "Type a depot code, name, or region…";
loadCurrentSession().then((restored) => {
  if (!restored) showWelcome();
  messageInput.focus();
});
