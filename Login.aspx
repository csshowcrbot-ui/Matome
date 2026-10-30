<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="Matome.Login" %>

<!DOCTYPE html>
<html lang="zh-TW">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>登入系統 - 間調 (Matome)</title>

    <!-- Google Fonts: Noto Sans TC & Inter -->
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&family=Noto+Sans+TC:wght@400;500;600&display=swap" rel="stylesheet">

    <!-- jQuery & SweetAlert2 -->
    <script src="https://code.jquery.com/jquery-3.6.4.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/sweetalert2@11"></script>

    <!-- 日式簡約極簡系統樣式 -->
    <link href="<%= ResolveUrl("~/Content/site.css") %>" rel="stylesheet" />

    <style>
        .login-page-bg {
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 24px;
            background: linear-gradient(135deg, #F9F8F6 0%, #EFECE6 100%);
        }
        .login-card {
            background-color: var(--color-surface);
            border: 1px solid var(--color-border);
            border-radius: var(--radius-md);
            box-shadow: var(--shadow-modal);
            width: 440px;
            max-width: 100%;
            padding: 40px;
        }
        .demo-pill-btn {
            background-color: var(--color-surface-soft);
            border: 1px solid var(--color-border);
            border-radius: var(--radius-sm);
            padding: 8px 12px;
            font-size: 12px;
            color: var(--color-text-primary);
            cursor: pointer;
            text-align: left;
            transition: all 0.2s ease;
            width: 100%;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }
        .demo-pill-btn:hover {
            border-color: var(--color-brand-primary);
            background-color: #F5EFEB;
            color: var(--color-brand-primary);
        }
    </style>
</head>
<body class="login-page-bg">
    <form id="form1" runat="server">
        <div class="login-card">
            <div style="text-align: center; margin-bottom: 28px;">
                <div class="brand-icon" style="width: 44px; height: 44px; font-size: 20px; margin: 0 auto 12px auto; border-radius: 8px;">
                    間
                </div>
                <h1 style="font-size: 20px; font-weight: 600; color: var(--color-text-primary);">
                    間調 (Matome)
                </h1>
                <div style="font-size: 13px; color: var(--color-text-secondary); margin-top: 4px;">
                    會議室及設備借用系統 · 請輸入同仁帳號與密碼登入
                </div>
            </div>

            <% if (!string.IsNullOrEmpty(ErrorMessage)) { %>
                <div style="padding: 10px 14px; margin-bottom: 20px; border-radius: var(--radius-sm); background: var(--color-accent-araigaki-light); color: var(--color-accent-araigaki); border: 1px solid #E6B5B1; font-size: 13px;">
                    ⚠️ <%= ErrorMessage %>
                </div>
            <% } %>

            <div class="form-group">
                <label class="form-label">登入帳號 (Account)</label>
                <asp:TextBox ID="txtAccount" runat="server" CssClass="form-control" placeholder="帳號"></asp:TextBox>
            </div>

            <div class="form-group" style="margin-bottom: 24px;">
                <label class="form-label">密碼 (Password)</label>
                <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" placeholder="密碼"></asp:TextBox>
            </div>

            <asp:Button ID="btnLogin" runat="server" CssClass="btn btn-primary" style="width: 100%; padding: 10px; font-size: 15px;" Text="登入系統" OnClick="btnLogin_Click" />
        </div>
    </form>
</body>
</html>
