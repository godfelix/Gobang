using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TCP_Server
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        //公用變數宣告
        TcpListener Server;//伺服端網路監聽器
        Socket Client;//客戶用的連線物件
        Thread ServerThread;//伺服器監聽用執行緒
        Thread ClientThread;//客戶用的通話執行緒
        Hashtable HashTable = new Hashtable();//儲存客戶名稱與通訊物件(雜湊表)(key:Name, Socket)
        //幫兩個人開一個遊戲房間
        Hashtable UserToRoom = new Hashtable();
        Hashtable Room = new Hashtable();
        //等待佇列
        Queue WaitingQueue = new Queue();

        private long GetRoomID()
        {
            return DateTime.Now.ToBinary();
        }

        private void CreateRoom(string user1, string user2)
        {
            long roomID = GetRoomID(); // 應該是不會一樣八
            UserToRoom.Add(user1, roomID);
            UserToRoom.Add(user2, roomID);
            string[] room = { user1, user2 };
            Room.Add(roomID, room);
        }
        private void TryMatch()
        {
            if (WaitingQueue.Count >= 2)//決定先後順序
            {
                string Player1 = (string)WaitingQueue.Dequeue();
                string Player2 = (string)WaitingQueue.Dequeue();
                CreateRoom(Player1, Player2);
                //TextBox4.Text += $"創建新房間 to ({Player1}, {Player2})" + "\r\n";
                TextBox4Controller($"Create New Room to ({Player1}, {Player2})");

                // 決定先後
                Random rd = new Random();
                int ranNum = rd.Next(1, 3);
                string String1 = "3001"; // 001 你事先手
                string String2 = "3002"; // 002 你是後手

                if (ranNum == 1)
                {
                    SendTo(String1 + "|" + Player2, Player1);
                    TextBox4Controller("Send 001 to " + Player1);
                    SendTo(String2 + "|" + Player1, Player2);
                    TextBox4Controller("Send 002 to " + Player2);
                }
                else if (ranNum == 2)
                {
                    SendTo(String1 + "|" + Player1, Player2);
                    TextBox4Controller("Send 001 to " + Player2);
                    SendTo(String2 + "|" + Player2, Player1);
                    TextBox4Controller("Send 002 to " + Player1);
                }
            }
        }
        private string Enemy(string user) // 會出現可能還沒有創room但是人就已經走的情況出現
        {
            //TextBox4.Text += UserToRoom[user].ToString() + "\r\n";
            if (UserToRoom.ContainsKey(user))
            {
                if (((string[])Room[(long)UserToRoom[user]])[0] == user)
                {
                    return (((string[])Room[(long)UserToRoom[user]]))[1];
                }
                else
                {
                    return (((string[])Room[(long)UserToRoom[user]]))[0];
                }
            }
            else
            {
                return "";
            }
        }
        //顯示本機IP
        private void Form1_Load(object sender, EventArgs e)
        {
            TextBox1.Text = MyIP(); //呼叫函數找本機IP    
        }
        //找出本機IP
        private string MyIP()
        {
            string hn = Dns.GetHostName();
            IPAddress[] ip = Dns.GetHostEntry(hn).AddressList; //取得本機IP陣列
            foreach (IPAddress it in ip)
            {
                if (it.AddressFamily == AddressFamily.InterNetwork)
                {
                    return it.ToString();//回傳IPv4字串
                }
            }
            return ""; //找不到合格IP回傳空字串
        }
        //啟動監聽連線要求
        private void Button1_Click(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false; //忽略跨執行緒處理的錯誤(允許跨執行緒存取變數)
            ServerThread = new Thread(ServerSub); //宣告監聽執行緒(ServerSub)
            ServerThread.IsBackground = true; //設定為背景執行緒
            ServerThread.Start(); //啟動監聽執行緒
            Button1.Enabled = false; 
        }
        //接受客戶連線要求的程式，針對每一客戶會建立一個獨立執行緒
        private void ServerSub()
        {
            IPEndPoint EP = new IPEndPoint(IPAddress.Parse(TextBox1.Text), 2023); //Server IP 和 Port
            Server = new TcpListener(EP); //建立伺服端監聽器
            Server.Start(100); //設定允許最多連線數100人
            while (true)
            {
                Client = Server.AcceptSocket(); //建立此客戶的連線物件Client
                ClientThread = new Thread(Listen); //建立監聽該客戶連線的獨立執行緒
                ClientThread.IsBackground = true; 
                //設定為背景執行
                //背景線程會隨進程結束而結束。
                //前台線程會阻止進程結束，如果該前台線程沒有結束他的工作。
                ClientThread.Start(); //開始執行緒運作
                //ClientThread.Resume();j
            }
        }
        //監聽客戶訊息的程式
        private void Listen()
        {
            Socket socket = Client;//複製Client通訊物件到個別客戶專用物件socket 
            Thread Thread = ClientThread;//複製執行緒ClientThread到區域變數Thread
            bool threadLife = true;
            while (threadLife) //持續監聽客戶傳來的訊息
            {
                try //用 socket 來接收此客戶訊息，ByteLength 是接收訊息的 Byte 數目
                {
                    byte[] Byte = new byte[1023];    //建立接收資料用的陣列，長度須大於可能的訊息
                    int ByteLength = socket.Receive(Byte); //接收網路資訊(Byte陣列)
                    string Msg = Encoding.Unicode.GetString(Byte, 0, ByteLength); //翻譯實際訊息(長度為ByteLength)
                    string Cmd = Msg.Substring(0, 1); //取出命令碼 (第一個字)
                    string Str = Msg.Substring(1);    //取出命令碼之後的訊息
                    string[] SenderMsg;
                    String SenderName;
                    long roomID;
                    string[] splitter;

                    TextBox4Controller(Msg); //將收到的訊息打出來

                    switch (Cmd)
                    {
                        case "0"://有新使用者上線：新增使用者到名單中
                            HashTable.Add(Str, socket); //連線加入雜湊表，Key:使用者，Value:連線物件(Socket)
                            WaitingQueue.Enqueue(Str);
                            Listbox1.Items.Add(Str); //加入上線者名單
                            SendAll(OnlineList()); //將目前上線人名單回傳剛剛登入的人(包含他自己)
                            TryMatch();
                            break;
                        case "9":
                            //TextBox4.Text += Str + "玩家中離" + "\r\n";
                            TextBox4Controller(Str + "User AFK");
                            HashTable.Remove(Str); //移除使用者名稱為Name的連線物件

                            // 其實按邏輯，應該不需要後面WaitingQueue這個判斷，因為必然是他
                            if (WaitingQueue.Count != 0 && (string)WaitingQueue.Peek() == Str)
                            {
                                WaitingQueue.Dequeue(); // 因為兩個人一定會進入配對
                                // 所以我只要偵測這第一個人是不是
                                // 還沒有連線到就直接中離，是的話就刪掉。
                            }
                            else if (Enemy(Str) != "")
                            {
                                // 直接將對手排進去queue裡面;
                                // 先排錯，看邏輯有沒有錯誤，因為我這邊是覺得，只要進得到這邊，就代表一定有對手。
                                // 基於我會把我關掉視窗後的對手排進去Waiting Queue裡面。
                                SendTo("9", Enemy(Str));
                                WaitingQueue.Enqueue(Enemy(Str));

                                roomID = (long)UserToRoom[Str];
                                UserToRoom.Remove(Enemy(Str));
                                UserToRoom.Remove(Str);
                                Room.Remove(roomID);
                            }
                            //最後一種可能就是打完結束，所以沒有出現在WaitingQueue裡面屬於正常，
                            //打完結束就把他從線上名單上刪除即可
                            
                            Listbox1.Items.Remove(Str); //自上線者名單移除Name
                            SendAll(OnlineList()); //將目前上線人名單回傳給使用者
                            TryMatch();
                            //按理來講，因為Thread的事情完成後，就會自動收回這個線程，
                            //而這個Thread就是在執行無限迴圈的Listen，
                            //我只要跳出去這個迴圈完成這個這個函式即可。
                            //不需要用到Abort();
                            //Thread.Abort(); //結束此客戶的執行緒 
                            threadLife = false;
                            break;
                        case "1"://使用者傳送訊息給所有人
                            splitter = Str.Split('|');
                            SendAll(Msg); //廣播訊息
                            TextBox4Controller("(public)" + splitter[0] + " by: " + splitter[1]);
                            break;
                        case "4": // 對方結束下琪，換下一個人
                            TextBox4Controller("Exchange" + Str);
                            SenderMsg = Str.Split('|');
                            SenderName = SenderMsg[0];
                            SendTo("5" + "Wait", SenderName);
                            SendTo("4" + SenderMsg[1], Enemy(SenderName));
                            break;
                        case "6": // 傳給敵人
                            SenderMsg = Str.Split('|');
                            SenderName = SenderMsg[0];
                            if (Enemy(SenderName) != "") SendTo("6" + SenderMsg[1], Enemy(SenderName));
                            else SendTo("7!Enemy has leaved, cannot Send messages!", SenderName);
                            break;
                        case "7":
                            SendTo("7(Process)Push you to the waiting Queue!!!", Str);
                            WaitingQueue.Enqueue(Str);
                            TryMatch();
                            break;
                        case "8":
                            TextBox4Controller(Str + "Win a game!!");
                            SendTo("8", Enemy(Str));

                            // 這邊先採取直接刪掉房間，如果之後需要，可以續戰之類的
                            roomID = (long)UserToRoom[Str];
                            UserToRoom.Remove(Enemy(Str));
                            UserToRoom.Remove(Str);
                            Room.Remove(roomID);

                            break;

                        case "/": // 搭配 case 1 2 
                            splitter = Str.Split(' ');
                            string anotherName = splitter[0];
                            string[] splitter2 = Msg.Substring(anotherName.Length + 2).Split('|');
                            if (HashTable.ContainsKey(anotherName)) SendTo("2" + splitter2[0] + " by: " + splitter2[1], anotherName);
                            else SendBack("7!Message cannot send, User is offline!", socket);
                            break;

                        default://使用者傳送私密訊息
                            string[] C = Str.Split('|'); //切開訊息與收件者
                            SendTo(Cmd + C[0], C[1]); //C[0]是訊息，C[1]是收件者
                            TextBox4Controller("(private)" + Str);
                            break;
                    }
                }
                catch(SocketException)
                {
                    threadLife = false;
                }
                catch (Exception)
                {
                    //debug用
                    TextBox4Controller("!!!Server Wrong Please Check!!!");
                    //有錯誤時忽略，通常是客戶端無預警強制關閉程式
                }
            }
        }

        private void TextBox4Controller(string str, string type = "")
        {
            TextBox4.Text += type + str + "\r\n";
            TextBox4.SelectionStart = TextBox4.Text.Length;
            TextBox4.ScrollToCaret();
        }
        //建立線上名單
        private string OnlineList()
        {
            string L = "L"; //代表線上名單的命令碼(字頭)
            for (int i = 0; i < Listbox1.Items.Count; i++)
            {
                L += Listbox1.Items[i]; //逐一將成員名單加入L字串
                //不是最後一個成員要加上","區隔
                if (i < Listbox1.Items.Count - 1) { L += ","; }
            }
            return L;
        }
        //傳送訊息給指定的客戶
        private void SendTo(string Str, string User)
        {
            byte[] Byte = Encoding.Unicode.GetBytes(Str); //訊息轉譯為Byte陣列
            Socket Sck = (Socket)HashTable[User]; //取出發送對象User的通訊物件
            Sck.Send(Byte, 0, Byte.Length, SocketFlags.None); //發送訊息
        }

        private void SendBack(string Str, Socket User)
        {
            byte[] Byte = Encoding.Unicode.GetBytes(Str);
            User.Send(Byte, 0, Byte.Length, SocketFlags.None);
        }
        //傳送訊息給所有的線上客戶
        private void SendAll(string Str)
        {
            byte[] B = Encoding.Unicode.GetBytes(Str); //訊息轉譯為Byte陣列
            foreach (Socket s in HashTable.Values) s.Send(B, 0, B.Length, SocketFlags.None); //傳送資料
        }
        //關閉視窗時
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.ExitThread();//關閉所有執行緒
        }

        private void TextBox4_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
