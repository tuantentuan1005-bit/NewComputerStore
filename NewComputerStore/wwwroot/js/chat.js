"use strict";

var connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .withAutomaticReconnect()
    .build();
var currentConversationId = null;

connection.onreconnecting(error => {
    console.warn("SignalR Reconnecting:", error);
    $("#chat-status").text("Đang kết nối lại...").removeClass("d-none text-success").addClass("text-warning");
});

connection.onreconnected(connectionId => {
    console.log("SignalR Reconnected");
    $("#chat-status").text("Đã kết nối").removeClass("text-warning").addClass("text-success");
    setTimeout(() => $("#chat-status").addClass("d-none"), 3000);
    
    // Re-join the conversation group after reconnecting
    if (currentConversationId) {
        connection.invoke("JoinConversation", currentConversationId)
            .catch(err => console.error("Error re-joining group:", err));
    }
});

connection.onclose(error => {
    console.error("SignalR Connection Closed:", error);
    $("#chat-status").text("Mất kết nối. Vui lòng F5.").removeClass("d-none text-success text-warning").addClass("text-danger");
});

connection.on("ReceiveMessage", function (msg) {
    if (msg.conversationId === currentConversationId) {
        appendMessage(msg);
        scrollToBottom();
        
        // Mark as read if popup is open
        if (!$("#chat-popup").hasClass("d-none")) {
            markAsRead(currentConversationId);
        }
    } else {
        // Increment global badge
        updateBadge(1);
    }
});

connection.on("UserTyping", function (conversationId, userName) {
    if (conversationId === currentConversationId) {
        $("#typing-user").text(userName);
        $("#typing-indicator").removeClass("d-none");
        setTimeout(() => $("#typing-indicator").addClass("d-none"), 3000);
    }
});

if (typeof window.currentUserId !== "undefined") {
    connection.start().then(function () {
        console.log("SignalR Connected");
        $("#chat-status").text("Đã kết nối").addClass("text-success").delay(3000).fadeOut();
    }).catch(function (err) {
        console.error("SignalR Start Error:", err.toString());
        $("#chat-status").text("Lỗi kết nối").addClass("text-danger");
        return console.error(err.toString());
    });
}

$(document).ready(function () {
    $("#chat-toggle-btn").click(function () {
        $("#chat-popup").toggleClass("d-none");
        if (!$("#chat-popup").hasClass("d-none")) {
            initChat();
            updateBadge(0, true);
        }
    });

    $("#chat-close-btn").click(function () {
        $("#chat-popup").addClass("d-none");
    });

    $("#chat-send-btn").click(sendMessage);
    $("#chat-input").keypress(function (e) {
        if (e.which == 13) sendMessage();
    });

    // Typing indicator
    $("#chat-input").on("input", function() {
        if (currentConversationId) {
            connection.invoke("SendTyping", currentConversationId, "Khách hàng").catch(err => console.error(err));
        }
    });
});

async function initChat() {
    if (!currentConversationId) {
        const res = await fetch("/Chat/GetConversation");
        const data = await res.json();
        currentConversationId = data.id;
        connection.invoke("JoinConversation", currentConversationId);
    }
    
    loadMessages();
    markAsRead(currentConversationId);
}

async function loadMessages() {
    const res = await fetch(`/Chat/GetMessages?conversationId=${currentConversationId}`);
    const messages = await res.json();
    $("#chat-messages").empty();
    messages.forEach(appendMessage);
    scrollToBottom();
}

function appendMessage(msg) {
    const isMine = msg.senderId == window.currentUserId;
    const messageClass = isMine ? "customer" : "staff";
    
    let contentHtml = "";
    if (msg.type === "ProductCard") {
        const product = JSON.parse(msg.metadata);
        contentHtml = `
            <div class="product-card-msg">
                <img src="/images/${product.image}" alt="${product.name}">
                <div class="product-card-body">
                    <div class="product-card-title">${product.name}</div>
                    <div class="product-card-price">${product.price}</div>
                    <a href="/Product/Details/${product.id}" class="btn btn-primary btn-sm w-100 mt-2 rounded-pill">Xem chi tiết</a>
                </div>
            </div>
        `;
    } else {
        contentHtml = `<div>${msg.content}</div>`;
    }

    const avatarHtml = isMine 
        ? `<div class="msg-avatar ms-2 bg-primary text-white rounded-circle d-flex align-items-center justify-content-center" style="width: 28px; height: 28px; font-size: 0.7rem; flex-shrink: 0;">KH</div>`
        : `<div class="msg-avatar me-2 bg-secondary text-white rounded-circle d-flex align-items-center justify-content-center" style="width: 28px; height: 28px; font-size: 0.7rem; flex-shrink: 0;"><i class="bi bi-person-fill"></i></div>`;

    const html = `
        <div class="chat-message ${messageClass}">
            ${!isMine ? avatarHtml : ""}
            <div class="message-content">
                <div class="message-bubble">
                    ${contentHtml}
                </div>
                <span class="message-time">${msg.sentAt}</span>
            </div>
            ${isMine ? avatarHtml : ""}
        </div>
    `;
    $("#chat-messages").append(html);
}

async function sendMessage() {
    const content = $("#chat-input").val();
    if (!content.trim() || !currentConversationId) return;

    const input = $("#chat-input");
    const btn = $("#chat-send-btn");
    
    input.prop("disabled", true);
    btn.prop("disabled", true).html('<span class="spinner-border spinner-border-sm"></span>');

    try {
        const res = await fetch("/Chat/SendMessage", {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: `conversationId=${currentConversationId}&content=${encodeURIComponent(content)}`
        });
        
        if (res.ok) {
            input.val("");
        } else {
            console.error("SendMessage failed");
            $("#chat-status").text("Lỗi gửi tin nhắn").removeClass("d-none").addClass("text-danger").show().delay(3000).fadeOut();
        }
    } catch (err) {
        console.error(err);
        $("#chat-status").text("Lỗi kết nối").removeClass("d-none").addClass("text-danger").show().delay(3000).fadeOut();
    } finally {
        input.prop("disabled", false).focus();
        btn.prop("disabled", false).html('<i class="bi bi-send-fill"></i>');
    }
}

function scrollToBottom() {
    const container = document.getElementById("chat-messages");
    container.scrollTop = container.scrollHeight;
}

function updateBadge(count, reset = false) {
    const badge = $("#chat-badge");
    if (reset) {
        badge.text("0").addClass("d-none");
    } else {
        const current = parseInt(badge.text()) + count;
        badge.text(current).removeClass("d-none");
    }
}

async function markAsRead(id) {
    await fetch(`/Chat/MarkRead?conversationId=${id}`, { method: "POST" });
}
