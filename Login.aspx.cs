using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using Matome.BLL;

namespace Matome
{
    public partial class Login : Page
    {
        private readonly UserManager _userMgr = new UserManager();
        public string ErrorMessage { get; set; }

        public Login()
        {
            ErrorMessage = string.Empty;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (AuthHelper.GetCurrentUser() != null)
            {
                Response.Redirect("~/Default.aspx");
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            string account = txtAccount.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
            {
                ErrorMessage = "請輸入帳號與密碼。";
                return;
            }

            var user = _userMgr.Authenticate(account, password);
            if (user != null)
            {
                AuthHelper.SignIn(user);
                Response.Redirect("~/Default.aspx");
            }
            else
            {
                ErrorMessage = "帳號或密碼不正確，或該帳號已被停用。";
            }
        }

        protected void btnQuickLogin_Click(object sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                string account = btn.CommandArgument;
                var allUsers = _userMgr.GetAllUsers();
                var target = allUsers.Find(u => string.Equals(u.Account, account, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    AuthHelper.SignIn(target);
                    Response.Redirect("~/Default.aspx");
                }
            }
        }
    }
}
