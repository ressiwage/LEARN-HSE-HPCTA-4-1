using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MSMQ
{
    public partial class frmMain : Form
    {
        private MessageQueue q = null;      // очередь сообщений, в которую будет производиться запись сообщений
        private Thread t = null;                // поток, отвечающий за работу с очередью сообщений
        private MessageQueue qRec = null;          // очередь сообщений
        private bool _continue = true;          // флаг, указывающий продолжается ли работа с мэйлслотом

        private string path, nick;
        public static bool IsBasicLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }
        public static bool FormatValid(string format)
        {
            if (format.Length == 0) return false;
            foreach (char c in format)
            {
                // This is using String.Contains for .NET 2 compat.,
                //   hence the requirement for ToString()
                if (!IsBasicLetter(c))
                    return false;
            }

            return true;
        }
        public static String getPipeValidName()
        {
            Form prompt = new Form()
            {
                Width = 500,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "введите никнейм",
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 50, Top=20, Text="введите никнейм", Width=400 };
            TextBox textBox = new TextBox() { Left = 50, Top=50, Width=400 };
            Button confirmation = new Button() { Text = "Ok", Left=350, Width=100, Top=70 };
            confirmation.Click += (sender, e) => {
                if (FormatValid(textBox.Text))
                {
                    prompt.Close();
                }
                else
                {
                    textLabel.Text = "Некорректный ввод. Никнейм должен содержать только латинские буквы";
                    textLabel.ForeColor = Color.Red;
                }
            };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.ShowDialog();
            String nickname = textBox.Text == "" ? "anon" : textBox.Text;
            return nickname;
        }

        private void ReceiveMessage()
        {
            if (qRec == null)
                return;

            System.Messaging.Message msg = null;

            // входим в бесконечный цикл работы с очередью сообщений
            while (_continue)
            {
                if (qRec.Peek() != null)   // если в очереди есть сообщение, выполняем его чтение, интервал до следующей попытки чтения равен 10 секундам
                    msg = qRec.Receive(TimeSpan.FromSeconds(10.0));

                messagesTB.Invoke((MethodInvoker)delegate
                {
                    if (msg != null)
                        messagesTB.Text += "\n >> " + msg.Label + " : " + msg.Body;     // выводим полученное сообщение на форму
                });
                Thread.Sleep(500);          // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
            }
        }

        // конструктор формы
        public frmMain()
        {
            InitializeComponent();
            this.nick = getPipeValidName();
            nickBox.Text = "ваш ник: " + this.nick;
            string path = Dns.GetHostName() + "\\private$\\" + this.nick;    // путь к очереди сообщений, Dns.GetHostName() - метод, возвращающий имя текущей машины
            this.path=path;

            // если очередь сообщений с указанным путем существует, то открываем ее, иначе создаем новую
            if (MessageQueue.Exists(path))
                qRec = new MessageQueue(path);
            else
                qRec = MessageQueue.Create(path);

            // задаем форматтер сообщений в очереди
            qRec.Formatter = new XmlMessageFormatter(new Type[] { typeof(String) });
            Thread t = new Thread(ReceiveMessage);
            t.Start();

        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (MessageQueue.Exists(tbPath.Text))
            {
                // если очередь, путь к которой указан в поле tbPath существует, то открываем ее
                q = new MessageQueue(tbPath.Text);
                btnSend.Enabled = true;
                btnConnect.Enabled = false;
            }
            else
                MessageBox.Show("Указан неверный путь к очереди, либо очередь не существует");
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            // выполняем отправку сообщения в очередь
            q.Send(this.path+" <:> " + this.nick +" <:> " +tbMessage.Text, Dns.GetHostName());
        }


        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _continue = false;      // сообщаем, что работа с очередью сообщений завершена

            if (t != null)
            {
                t.Abort();          // завершаем поток
            }

            if (q != null)
            {
                //MessageQueue.Delete(q.Path);      // в случае необходимости удаляем очередь сообщений
            }
        }
    }
}