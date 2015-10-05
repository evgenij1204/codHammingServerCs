using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Threading;

namespace codHammingServerCs
{
    public partial class Form1 : Form
    {
        protected Thread thread;
        delegate void SetTextCallback(string str);
        public Form1()
        {
            InitializeComponent();
            thread = new Thread(функция_потока);
            thread.Start();

        }
        protected void функция_потока()
        {
            TcpListener listner = null;
            try
            {
                listner = new TcpListener(5595);
                listner.Start();
                byte[] data = new byte[512];
                string str;
                PrintMsg("Wait client");
                while (true)
                {
                    if (listner.Pending())
                    {
                        str = null;
                        TcpClient client = listner.AcceptTcpClient();
                        PrintMsg("Connected");
                        NetworkStream stream = client.GetStream();
                        int i;
                        byte[] msgbyte = new byte[512];
                        while ((i = stream.Read(data, 0, data.Length)) != 0)
                        {
                            for (int j = 0; j < i; j++)
                                if (data[j] == 1) str += 1;
                                else
                                    str += 0;
                        }
                        PrintMsg("Полученное закодированное сообщение в двоичном формате                   :\n" + str);
                        byte[] vec = new byte[str.Length];
                        for (int j = 0; j < str.Length; j++)
                            if (str[j] == '1') vec[j] = 1;
                            else
                                vec[j] = 0;
                        Hamming_Decoder hd = new Hamming_Decoder(vec);
                        hd.добавитьОшибку();
                        byte[] vec2 = hd.вернутьДанныеБезДекодирования();
                        PrintMsg("Полученное закодированное сообщение в двоичном формате с ошибкой:\n" + вСтроку(vec2));
                        vec2 = hd.декодировать(0);
                        PrintMsg("Разкодированное сообщение с ошибкой:\n" + конвертировать(vec2));
                        //PrintMsg();
                        vec = hd.декодировать(1);
                        PrintMsg("Разкодированное сообщение с исправленной ошибкой:\n" + конвертировать(vec));
                        client.Close();
                        PrintMsg("Wait client");
                    }
                }
            }
            catch (SocketException e) { PrintMsg(e.Message); }
            finally { listner.Stop(); }
        }
        private string вСтроку(byte[] v)
        {
            string s = "";
            for (int i = 0; i < v.Length; i++)
                s += v[i];
            return s;
        }
        private string конвертировать(byte[] вектор)
        {
            string tmp = "";
            int итератор1 = 0, итератор2 = 0;
            byte[] res = new byte[вектор.Length / 7];
            for (int i = 0; i < вектор.Length; i++)
            {
                tmp += вектор[i];
                итератор1++;
                if (итератор1 == 7)
                {
                    res[итератор2] = Convert.ToByte(tmp, 2);
                    tmp = "";
                    итератор1 = 0;
                    итератор2++;
                }
            }
            return System.Text.Encoding.UTF32.GetString(res);//System.Text.Encoding.ASCII.GetString(res);;
        }
        private void PrintMsg(string str)
        {
            if (this.listBox1.InvokeRequired)
            {
                SetTextCallback callback = new SetTextCallback(PrintMsg);
                this.Invoke(callback, new object[] { str });
            }
            else { this.listBox1.Items.Add(str); }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            thread.Abort();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }
    }
    abstract class Hamming_Base
    {
        protected Hamming_Base(byte[] входнойВектор)
        {
            вычислитьРазмерМатрицы(входнойВектор);
            создатьВременныйВектор(входнойВектор);
            создатьМатрицуПреобразования();
        }
        protected int строк, столбцов, проверочных_ячеек;
        protected byte[,] матрица_преобразования;
        protected byte[] временныйВектор;
        protected void вычислитьРазмерМатрицы(byte[] входнойВектор)
        {
            проверочных_ячеек = количествоПроверочныхЯчеек(входнойВектор);
            строк_и_столбцов(входнойВектор);
        }
        abstract protected void строк_и_столбцов(byte[] вектор);
        abstract protected int количествоПроверочныхЯчеек(byte[] вектор);
        protected void создатьМатрицуПреобразования()
        {
            матрица_преобразования = new byte[строк, столбцов];
            for (int i = 0; i < столбцов; i++)
            {
                string строка = Convert.ToString(i + 1, 2);
                int номер_строки = 0;
                if (строка.Length == 1) матрица_преобразования[номер_строки, i] = 1;
                else
                    for (int j = строка.Length; j != 0; j--)
                    {
                        char ch = строка[j - 1];
                        if (ch == '1') матрица_преобразования[номер_строки, i] = 1;
                        номер_строки++;
                    }
            }
        }
        abstract protected void создатьВременныйВектор(byte[] вектор);
        protected void умножитьВекторНаМатрицу(ref byte[] вектор_столбец)
        {
            for (int i = 0; i < строк; i++)
            {
                for (int j = 0; j < столбцов; j++)
                {
                    вектор_столбец[i] += (byte)(матрица_преобразования[i, j] * временныйВектор[j]);
                }
                вектор_столбец[i] = (byte)(вектор_столбец[i] % 2);
            }
        }
    }
    class Hamming_Decoder : Hamming_Base
    {
        public Hamming_Decoder(byte[] входной_вектор) : base(входной_вектор) { }
        protected override void строк_и_столбцов(byte[] вектор)
        {
            строк = проверочных_ячеек;
            столбцов = вектор.Length;
            // - проверочных_ячеек];
        }
        protected override int количествоПроверочныхЯчеек(byte[] вектор)
        {
            double длинаВектора = (double)вектор.Length;
            int счетчик = 0;
            while (длинаВектора >= 2)
            {
                длинаВектора /= 2;
                счетчик++;
            }
            счетчик++;
            return счетчик;
        }
        protected override void создатьВременныйВектор(byte[] вектор)
        {
            временныйВектор = вектор;
        }
        public byte[] вернутьДанныеБезДекодирования()
        {
            return временныйВектор;
        }
        public byte[] декодировать(int error)//1-с учетом ошибки, 0 - без учета ошибки
        {
            if (error == 1)
            {
                byte[] вектор_синдромов = new byte[проверочных_ячеек];
                умножитьВекторНаМатрицу(ref вектор_синдромов);
                string s = "";
                for (int i = (проверочных_ячеек - 1); i > -1; i--)
                    s += вектор_синдромов[i].ToString();
                int позиция_ошибки = Convert.ToInt32(s, 2);
                if (позиция_ошибки > 0)
                {
                    if (временныйВектор[позиция_ошибки - 1] == 0)
                        временныйВектор[позиция_ошибки - 1] = 1;
                    else
                        временныйВектор[позиция_ошибки - 1] = 0;
                }
            }
            int счетчик = 0;
            byte[] результат = new byte[временныйВектор.Length - проверочных_ячеек];
            for (int i = 0; i < временныйВектор.Length; i++)
            {
                if (!((i + 1).Equals((int)Math.Pow(2, счетчик))))
                {
                    результат[i - счетчик] = временныйВектор[i];

                }
                else
                {
                    счетчик++;
                }
            }
            return результат;
        }
        public void добавитьОшибку()
        {
            Random ran = new Random();
            int num = ran.Next(0, временныйВектор.Length);
            if (временныйВектор[num] == 0)
                временныйВектор[num] = 1;
            else
                временныйВектор[num] = 0;
        }
    }
}
