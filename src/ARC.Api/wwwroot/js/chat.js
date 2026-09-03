const APP_NAME = "ARC Assistant";

// Clear any old onboarding session from previous chat versions.
try { localStorage.removeItem("arc.chat.session"); } catch { /* ignore */ }

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

function appendMessage(text, type, agent = APP_NAME, replyFormat = "text") {
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
  meta.textContent = type === "sent" ? formatTime(new Date()) : `${agent} · ${formatTime(new Date())}`;
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

function updateSessionSummary() {
  sessionSummary.innerHTML = `
    <div><dt>Mode</dt><dd>Depot lookup</dd></div>
    <div><dt>Source</dt><dd>depot_mstr</dd></div>
    <div><dt>Setup</dt><dd>Not required</dd></div>
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
  headerStatus.innerHTML = '<span class="status-dot"></span>Online · ARC Assistant';
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

async function sendApiMessage(text) {
  showTyping();
  sendBtn.disabled = true;

  try {
    const response = await fetch("/v1/chat/messages", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Arc-Upn": "chat@local.dev",
        "X-Arc-Role": "Legal",
      },
      body: JSON.stringify({ message: text }),
    });

    hideTyping();

    if (!response.ok) {
      const err = await response.json().catch(() => ({}));
      appendMessage(err.error || `Request failed (${response.status})`, "received");
      return;
    }

    const data = await response.json();
    appendMessage(data.reply, "received", data.agent || APP_NAME, data.replyFormat || "text");
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

function resetChat() {
  chatThread.innerHTML = "";
  closeSidebar();
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
showWelcome();
messageInput.focus();
