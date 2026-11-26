namespace HotelComplexApp;

partial class LoginForm
{
    private System.ComponentModel.IContainer? components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblLogin = new Label();
        txtLogin = new TextBox();
        lblPassword = new Label();
        txtPassword = new TextBox();
        btnLogin = new Button();
        btnExit = new Button();
        SuspendLayout();
        // 
        // lblLogin
        // 
        lblLogin.AutoSize = true;
        lblLogin.Location = new Point(34, 33);
        lblLogin.Name = "lblLogin";
        lblLogin.Size = new Size(52, 20);
        lblLogin.TabIndex = 0;
        lblLogin.Text = "Логин";
        // 
        // txtLogin
        // 
        txtLogin.Location = new Point(34, 60);
        txtLogin.Margin = new Padding(3, 4, 3, 4);
        txtLogin.Name = "txtLogin";
        txtLogin.Size = new Size(319, 27);
        txtLogin.TabIndex = 0;
        // 
        // lblPassword
        // 
        lblPassword.AutoSize = true;
        lblPassword.Location = new Point(34, 113);
        lblPassword.Name = "lblPassword";
        lblPassword.Size = new Size(62, 20);
        lblPassword.TabIndex = 1;
        lblPassword.Text = "Пароль";
        // 
        // txtPassword
        // 
        txtPassword.Location = new Point(34, 140);
        txtPassword.Margin = new Padding(3, 4, 3, 4);
        txtPassword.Name = "txtPassword";
        txtPassword.Size = new Size(319, 27);
        txtPassword.TabIndex = 1;
        txtPassword.UseSystemPasswordChar = true;
        // 
        // btnLogin
        // 
        btnLogin.Location = new Point(34, 207);
        btnLogin.Margin = new Padding(3, 4, 3, 4);
        btnLogin.Name = "btnLogin";
        btnLogin.Size = new Size(154, 47);
        btnLogin.TabIndex = 2;
        btnLogin.Text = "Войти";
        btnLogin.UseVisualStyleBackColor = true;
        btnLogin.Click += btnLogin_Click;
        // 
        // btnExit
        // 
        btnExit.Location = new Point(200, 207);
        btnExit.Margin = new Padding(3, 4, 3, 4);
        btnExit.Name = "btnExit";
        btnExit.Size = new Size(154, 47);
        btnExit.TabIndex = 3;
        btnExit.Text = "Выход";
        btnExit.UseVisualStyleBackColor = true;
        btnExit.Click += btnExit_Click;
        // 
        // LoginForm
        // 
        AcceptButton = btnLogin;
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(389, 287);
        Controls.Add(lblLogin);
        Controls.Add(txtLogin);
        Controls.Add(lblPassword);
        Controls.Add(txtPassword);
        Controls.Add(btnLogin);
        Controls.Add(btnExit);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Margin = new Padding(3, 4, 3, 4);
        MaximizeBox = false;
        Name = "LoginForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Вход в систему";
        Load += LoginForm_Load;
        ResumeLayout(false);
        PerformLayout();
    }

    private Label lblLogin;
    private TextBox txtLogin;
    private Label lblPassword;
    private TextBox txtPassword;
    private Button btnLogin;
    private Button btnExit;
}