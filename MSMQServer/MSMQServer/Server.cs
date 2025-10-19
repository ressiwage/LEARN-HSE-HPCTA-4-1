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
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MSMQ
{
    public partial class frmMain : Form
    {
        private MessageQueue q = null;          // очередь сообщений
        private Thread t = null;                // поток, отвечающий за работу с очередью сообщений
        private bool _continue = true;          // флаг, указывающий продолжается ли работа с мэйлслотом
        private List<string> clients = new List<string>();
        private string pipesToText(List<string> clients)
        {
            return String.Join("\n",
                                    (new List<string> { "participants" }).Concat(
                                        clients.Select(
                                            x => {
                                                string[] splitted = x.Split(new string[] { ":" }, StringSplitOptions.None);
                                                return splitted[1];
                                            }
                                            ).ToArray()
                                        ).ToArray());
        }

        public uint SendToPipe(string message, string pipe_)
        {
            string pipe = pipe_.Split(':')[0];
            Console.WriteLine(">>:" + pipe);
            MessageQueue cq = null;
            if (MessageQueue.Exists(pipe))
                cq = new MessageQueue(pipe);
            else
                return 0;
            cq.Formatter = new XmlMessageFormatter(new Type[] { typeof(String) });

            cq.Send(message);
            return 1;
        }

        // конструктор формы
        public frmMain()
        {
            InitializeComponent();
            string path = Dns.GetHostName() + "\\private$\\ServerQueue";    // путь к очереди сообщений, Dns.GetHostName() - метод, возвращающий имя текущей машины

            // если очередь сообщений с указанным путем существует, то открываем ее, иначе создаем новую
            if (MessageQueue.Exists(path))
                q = new MessageQueue(path);
            else
                q = MessageQueue.Create(path);

            // задаем форматтер сообщений в очереди
            q.Formatter = new XmlMessageFormatter(new Type[] { typeof(String) });

            // вывод пути к очереди сообщений в заголовок формы, чтобы можно было его использовать для ввода имени в форме клиента, запущенного на другом вычислительном узле
            this.Text += "     " + q.Path;

            // создание потока, отвечающего за работу с очередью сообщений
            Thread t = new Thread(ReceiveMessage);
            t.Start();
        }

        // получение сообщения
        private void ReceiveMessage()
        {
            if (q == null)
                return;

            System.Messaging.Message qmsg = null;

            // входим в бесконечный цикл работы с очередью сообщений
            while (_continue)
            {
                if (q.Peek() != null)   // если в очереди есть сообщение, выполняем его чтение, интервал до следующей попытки чтения равен 10 секундам
                    qmsg = q.Receive(TimeSpan.FromSeconds(10.0));

                rtbMessages.Invoke((MethodInvoker)delegate
                {
                    if (qmsg != null)
                    {
                        string msg = (string)qmsg.Body;
                        Console.WriteLine(msg);
                        string[] data = msg.Split(new string[] { " <:> " }, StringSplitOptions.None);
                        string clientpipename = data[0]+":"+data[1];
                        if (!clients.Contains(clientpipename))
                        {
                            clients.Add(clientpipename);
                            rtbParticipants.Text = pipesToText(clients);
                        }
                        DateTime dt = DateTime.Now;
                        string time = dt.Hour + ":" + dt.Minute+":"+dt.Second;

                        string message = "\n >> "  + data[0] + "|" + data[1] + "|" + time  + ":  " + data[2];                             // выводим полученное сообщение на форму
                        rtbMessages.Text += message;
                        List<string> delete = new List<string>();
                        foreach (string pipe in clients)
                        {
                            Console.WriteLine("?:>"+message+"<?:"+pipe);

                            if (SendToPipe(message, pipe) == 0)
                            {
                                delete.Add(pipe);
                            }
                        }
                        foreach (var pipe in delete)
                        {
                            clients.Remove(pipe);
                            rtbParticipants.Text = pipesToText(clients);
                        }
                    }
                });
                Thread.Sleep(500);          // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
            }
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