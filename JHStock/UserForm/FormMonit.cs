﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Tools;
using JHStock.Update;
using System.IO;
using Newtonsoft.Json;
using System.Threading;

namespace JHStock
{
    public partial class FormMonit : Form
    {
        public FormMonit( )
        {
            f = new Form1();
            selectstock = new List<Stock>();
            savestockinfor = new List<string>();
            //_stockdata = new StockData();
            InitializeComponent();
            InitMaDataTable();
            this.type = "MA";
            Ready = Init();
            isinitdatarunning = false;
            bCompute = false;
        }
        private void FormMonit_Load(object sender, EventArgs e)
        {
            if (Ready)
            {
                if (!isinitdatarunning)
                {
                    isinitdatarunning = true;                   
                    System.Threading.Thread nonParameterThread =
                        new Thread(new ThreadStart(InitData));
                    nonParameterThread.Start();
                }
                return;
            }
            else
            {
                this.Hide();
                MessageBox.Show("显示form1.从新配置");
                this.Show();
            }
        }
        private bool Init()
        {
            // init _jscfg and _stocks
            string filename = "jsconfig.ini";
            if (!(filename != "" && File.Exists(filename)))
            {
                MessageBox.Show("配置文件不存在，请检查后重置");
                return false;
            }

            JSConfig jscfg = new JSConfig();
            jscfg.Load(filename);
            BaseConfig cfg = jscfg.baseconfig;
            MFile.cfg = cfg;
            if (!cfg.CheckWorkPath())
                MessageBox.Show("工作目录不存在，请手工创建" + cfg.NowWorkPath());

            if (!jscfg.globalconfig.InitStocks())
            {
                MessageBox.Show(_jscfg.globalconfig.ErrMsg); //退出
                MessageBox.Show("配置文件不正确，请检查后重置");
                return false;
            }
            this._jscfg = jscfg;
            this._stocks = jscfg.globalconfig.Stocks;

            //init DebugStocks
            string importtext = "600221";
            if (File.Exists("select.txt"))
                importtext = File.ReadAllText("select.txt").Trim();
            List<Stock> find = _stocks.stocks.FindAll(r => importtext.Contains(r.Code.Substring(2, 6))).ToList();
            if (find.Count > 0)
                DebugStocks = find;

            //for _stockdata
            _stockdata = new StocksData(_jscfg);
            
            return true;
        }
        private void InitData() //采用线程控制运行
        {
            _stockdata.ThreadShowMsg = ThreadShowMsg;
            _stockdata.ActionMsg = ThreadActionMsg;
            initdataaction = _stockdata.InitData();
            if (initdataaction.StartsWith("Quit"))
            {
                ThreadShowMsg ("数据错误：" + initdataaction);
                MessageBox.Show("数据错误：" + initdataaction); // 无法获取数据，请检查网络 或从新设置
                bCompute = false;
                return;
            }
            if (initdataaction.StartsWith("OK"))
                ThreadShowMsg("数据正常" + initdataaction);  //
            bCompute = true;

        }
        private void LoadCfg()
        {
            string filename = "jsconfig.ini";
            if (File.Exists(filename))
            {
                _jscfg.Load(filename);

            }
        }

        private void buttonAddToTXDBlock_Click(object sender, EventArgs e)
        {
            ((Button)sender).Enabled = false;
            string str = "";
            foreach (DataGridViewRow dr in dgv.Rows)
            {

                string code = (string)dr.Cells["代码"].Value;
                if (code != null && code.StartsWith("SZ"))
                    str += "\r\n0" + code.Substring(2);
                else if (code != null && code.StartsWith("SH"))
                    str += "\r\n1" + code.Substring(2);
            }

            MFile.WriteAllText(_stocks.Gcfg.Baseconfig.BlockPath(), str);
            MessageBox.Show("已输出");
            ((Button)sender).Enabled = true;
        }
        private void buttonToTxt_Click(object sender, EventArgs e)
        {
            StringBuilder outstr = DataTableToString(dt);
            string filesavename = Tools.TimeStringTools.NowDateMin() + "_Monit" + type + ".txt";
            if (checkBoxTable.Checked)
                outstr.Replace(',', '\t');
            MFile.AppendAllText(filesavename, outstr.ToString());
            MessageBox.Show("已输出到文件中：" + filesavename);
        }
        private void buttonReCompute_Click(object sender, EventArgs e)
        {
            if (!bCompute) return;
            selectstock.Clear();
            savestockinfor.Clear();

            if(checkBoxUserDefinitionStocks.Checked)
              foreach (Stock s in DebugStocks)
                TestStock(s,checkBoxDebugOutPut.Checked);          
            else
              foreach (Stock s in _stocks.stocks)
                TestStock(s);    
            ShowSelectedStocks();
        }

        // Monit for Compute  and show
        private void ShowSelectedStocks()
        {
            dt.Rows.Clear();
            int i = 0;
            foreach (Stock s in selectstock)
            {
                DataRow dr = dt.NewRow();
                dr["名称"] = s.Name;
                dr["代码"] = s.Code;
                string[] ss = savestockinfor[i].Split(new string[] { "\t" }, StringSplitOptions.RemoveEmptyEntries);
                if (ss.Length == 3)
                {
                    dr["持续天数"] = ss[0];
                    dr["后续天数"] = ss[1];
                    dr["后续天数的情况"] = ss[2];
                }
                dt.Rows.Add(dr);
                i++;
            }
        }
        private void InitMaDataTable()
        {
            dt = new DataTable();
            List<string> columntitles = new List<string>() { "名称", "代码", "日期", "持续天数" ,"后续天数","后续天数的情况" };//,"杂项"
            //columntitles = new List<string>() { "名称", "代码","杂项" };//,"杂项"
            for (int count = 0; count < columntitles.Count; count++)
            {
                DataColumn dc = new DataColumn(columntitles[count]);
                if ("代码名称状态杂项日期".Contains(columntitles[count]))
                {
                    dc.DataType = typeof(string);
                }
                else if ("上一状态持续天数后续天数".Contains(columntitles[count]))
                {
                    dc.DataType = typeof(int);
                }
                else
                {
                    dc.DataType = typeof(string);
                    //dc.MaxLength = 30;
                }
                dt.Columns.Add(dc);
            }
            dgv.DataSource = dt; // 宽度应设置在 dgv 上

            for (int i = 0; i < dgv.ColumnCount; i++)
            {
                if ("代码名称状态杂项日期".Contains( dgv.Columns[i].Name))
                {
                    dgv.Columns[i].Width = 60;
                }
                else if ("上一状态持续天数后续天数".Contains(dgv.Columns[i].Name))
                {
                    dgv.Columns[i].Width = 40;
                }
                else
                {
                    dgv.Columns[i].Width = 150;
                    //dc.MaxLength = 30;
                }
            }
        }
        private StringBuilder DataTableToString(DataTable dtsave)
        {
            StringBuilder outstr = new StringBuilder();
            foreach (DataColumn dc in dtsave.Columns)
            {
                outstr.Append(dc.ColumnName + ",");
            }
            outstr.Append("\r\n");
            foreach (DataRow dr in dtsave.Rows)
            {
                foreach (DataColumn dc in dtsave.Columns)
                {
                    if (dc.DataType != typeof(Image))
                        outstr.Append(dr[dc.ColumnName] + ",");
                    else
                        outstr.Append("image,");

                }
                outstr.Append("\r\n");
            }
            outstr = outstr.Replace(",\r\n", "\r\n");
            return outstr;
        }
        //List<KData> listclose = kd.Skip(0).Take(60).ToList();
        public void TestStock(Stock s,bool DebugOutPut = false, int staticdaylenght = 200)
        {              
            if (!_stockdata.HasKdata(s.ID))
                return;
            double[] kdvol = _stockdata.GetKD(s.ID).Select(r =>(double)( r.vol)).ToArray();
            List<double> vma5 = MA(0, 5, kdvol);
            double now = vma5[0];
            List<double> dvma5rate  = vma5.Select( r =>
                { double ret = (r - now)/now; now = r; return ret; }).ToList();
            List<int> ma5L = dvma5rate.Select(r => (int)(r * 100 + (r > 0 ? 0.5 : -0.5))).ToList();
            //List<double> vdvma5rate = MA(0, 5, dvma5rate.ToArray());
            //List<int> intvdvma5rate = vdvma5rate.Select(r => (int)(r * 100 * 5 + (r > 0 ? 0.5 : -0.5))).ToList();
            List<Point> lines=
            HorizontalLines(ma5L, 18, 15, 12);  //firstbigbreak,secondbigbreak,thirdbigbreak
            lines = MergeLines(lines,ma5L,24); //合并条件1. 两条线只差一点 2.该点数值在  24  以内  //以后再优化
            if(lines.Count>0){
                Point LastLine = lines[lines.Count - 1];
                int epos = LastLine.X+LastLine.Y;
                int undays = ma5L.Count - epos;


                if(  ( !checkBoxMonitdays.Checked &&  undays < 5 && undays > 2 && Math.Abs(ma5L[epos + 1]) > 29 && Math.Abs(ma5L[epos + 2]) > 29)  //监视两天
                   || (checkBoxMonitdays.Checked && undays < 5 && undays > 1 && Math.Abs(ma5L[epos + 1]) > 29) )//一天为监视
                {
                    selectstock.Add(s);
                    // 持续天数  \t 后续天数  \t  后续天数的情况
                    savestockinfor.Add( LastLine.Y +"\t"+ undays+"\t"+ string.Join(",",ma5L.Skip(epos).Take(undays)));
                }
            }
            if (DebugOutPut)
            {
                List<int> L = ma5L.Select(r => 0).ToList();
                foreach (Point p in lines)
                    for (int i = 0; i < p.Y; i++)
                        L[i + p.X] = 1;
                string str = "\r\nvma5\t" + vma5.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)
                             + "\r\nma5L\t" + ma5L.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)
                             + "\r\nL\t" + L.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2);
                MFile.WriteAllText(s.Name + s.NumCode + ".txt", str);
            }
        }
        public void TestStock2(Stock s, int staticdaylenght = 200) // for Debug Test
        {
            if (!_stockdata.HasKdata(s.ID))
                return;
            // if select  then selectstockindex.add 
            selectstock.Add(s);
            int[] kdclose = _stockdata.GetKD(s.ID).Select(r => (r.close)).ToArray();
            int[] kdvol = _stockdata.GetKD(s.ID).Select(r => (r.vol)).ToArray();
            List<double> ma60 = MA(0, 60, kdclose); //skip 60
            List<double> ma20 = MA(40, 20, kdclose);
            List<double> ma10 = MA(50, 10, kdclose);
            List<double> ma5 = MA(55, 5, kdclose);

            List<double> vma10 = MA(50, 10, kdvol);
            List<double> vma5 = MA(55, 5, kdvol);

            double now = vma5[0];
            List<double> dvma5rate = vma5.Select(r =>
            { double ret = (r - now) / now; now = r; return ret; }).ToList();
            List<int> ma5L = dvma5rate.Select(r => (int)(r * 100 + (r > 0 ? 0.5 : -0.5))).ToList();


            List<double> vdvma5rate = MA(0, 5, dvma5rate.ToArray());
            List<int> intvdvma5rate = vdvma5rate.Select(r => (int)(r * 100 * 5 + (r > 0 ? 0.5 : -0.5))).ToList();

            List<Point> lines =
            HorizontalLines(ma5L, 18, 15, 12);  //firstbigbreak,secondbigbreak,thirdbigbreak
            lines = MergeLines(lines, ma5L, 24); //合并条件1. 两条线只差一点 2.该点数值在  24  以内  //以后再优化
            
            //for debug
            List<int> L = ma5L.Select(r => 0).ToList();
            foreach (Point p in lines)
                for (int i = 0; i < p.Y; i++)
                    L[i + p.X] = 1;
            string str = "date\tclose\tvol\n"
                         + _stockdata.GetKD(s.ID).Select(r => r.date + "\t" + r.close + "\t" + r.vol + "\n").Aggregate((r1, r2) => r1 + r2)
                         + "\r\nma60\t" + ma60.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)
                         + "\r\nma20\t" + ma20.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)
                         + "\r\nma10\t" + ma10.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)
                         + "\r\nma5\t" + ma5.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)
                         + "\r\nvma10\t" + vma10.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)
                         + "\r\nvma5\t" + vma5.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)

                         + "\r\ndvma5rate\t" + dvma5rate.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)
                         + "\r\nvdvma5rate\t" + vdvma5rate.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)
                         + "\r\nintvdvma5rate\t" + intvdvma5rate.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)
                         + "\r\nma5L\t" + ma5L.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2)
                         + "\r\nL\t" + L.Select(r => r + "\t").Aggregate((r1, r2) => r1 + r2);
            MFile.WriteAllText(s.Name + s.NumCode + ".txt", str);
        }
        private List<Point> MergeLines(List<Point> lines, List<int> maL, int bigbreak)
        {
            if(lines.Count<2) return lines;;
            List<Point> nlines = new List<Point>();
            Point L = lines[0];
            for (int i = 1; i < lines.Count; i++)
            {
                if (L.X + L.Y + 1 != lines[i].X || maL[lines[i].X - 1] > bigbreak)
                {
                    nlines.Add(L);
                    L = lines[i];
                }
                else
                {
                    L.Y += 1 + lines[i].Y;
                }
            }
            if (!nlines.Contains(L))
                nlines.Add(L);
            return nlines;
        }
        private List<Point> HorizontalLines(List<int> data, int firstbigbreak, int secondbigbreak, int thirdbigbreak)
        {

            Point line = new Point(0,0);
            int fc = 0, sc = 0, tc = 0,lc=0;
            List<Point> Lines =  new List<Point>(); // Size.X  StartPoint  Size.Y  lenght
            List<int> absdata = data.Select(r => Math.Abs(r)).ToList();
            if (absdata[0] <= firstbigbreak)
                lc++;
            for (int i = 0; i < absdata.Count; i++)
            {               
                if (absdata[i] > firstbigbreak)
                    fc++;
                else if (absdata[i] > secondbigbreak)
                    sc++;
                else if (absdata[i] > thirdbigbreak)
                    tc++;

                if (lc == 1 && i - line.X > 5)
                {
                    if (absdata[i - 5] > firstbigbreak)
                    {
                        if(fc>0) fc--;
                    }
                    else if (absdata[i - 5] > secondbigbreak)
                    {
                        if(sc>0) sc--;
                    }
                    else if (absdata[i - 5] > thirdbigbreak)
                    {
                        if(tc>0) tc--;
                    }
                }


                if (fc == 1 || sc == 2 || tc == 3 || sc + tc==3 )  //bbreak = true; 
                {
                    fc = sc = tc = 0;
                 
                    if (lc == 1)  // lc == 2  //lineend //五天以上
                    {
                        lc = 0;
                        line.Y = i - line.X;
                        if (line.Y > 4)
                            Lines.Add(line);
                    }
                }
                else
                {
                    if (lc == 0) // line 未开始状态
                    {
                        lc++;
                        line.X = i;
                    }
                }

            }
            return Lines;
        }
        double maxsublinear(List<double> a, out int b, out int e)
        {
            int i;
            int begin = 0, end = 0, lbegin = 0;
            double curSum = 0; ///* 当前序列和 //
            double maxSum = 0; ///* 最大序列和 //

            ////* 开始循环求子序列和 
            for (i = 0; i < a.Count; i++)
            {
                curSum = curSum + a[i];

                ///* 与最大子序列和比较，更新最大子序列和 /
                if (curSum > maxSum)
                {
                    maxSum = curSum;
                    end = i;
                    if (begin < end)
                        lbegin = begin;
                    else
                        lbegin = lbegin + 0;
                }

                ///* 动态规划部分，舍弃当前和为负的子序列 /
                if (curSum < 0)
                {
                    curSum = 0;
                    begin = i + 1 >= a.Count ? i : i + 1;

                }
            }
            b = lbegin;
            e = end;
            return maxSum;
        }        
        //type = 0: close  type = 1: vol type =  2: open type=3 high  type=4 low
        private List<double> MA(int skip, int daylength, int[] listdata)
        {
            List<double> MA = new List<double>();
            double sum = listdata.Skip(skip).Take(daylength).Sum();
            for (int i = skip + daylength; i < listdata.Length; i++)
            {
                double avg = sum * 1.0 / daylength;
                MA.Add(avg);
                sum += listdata[i] - listdata[i - daylength];
            }
            return MA;
        }
        private List<double> MA(int skip, int daylength, double[] listdata)
        {
            List<double> MA = new List<double>();
            double sum = listdata.Skip(skip).Take(daylength).Sum();
            for (int i = skip + daylength; i < listdata.Length; i++)
            {
                double avg = sum * 1.0 / daylength;
                MA.Add(avg);
                sum += listdata[i] - listdata[i - daylength];
            }
            return MA;
        }
        
        private JSConfig _jscfg;
        private Stocks _stocks;
        private string type;
        private DataTable dt;
        private StocksData _stockdata;
        private List<Stock> selectstock;
        private List<string> savestockinfor; //与selectstock 同步
        private bool Ready;
        private bool bCompute;
        Form1 f;
        public List<Stock> DebugStocks;

        private void buttonConfig_Click(object sender, EventArgs e)
        {
            this.Hide();
            f.ShowDialog();
            this.DebugStocks = f.StocksByItemsShow();
            this.Show();
        }
        private void buttonCheckData_Click(object sender, EventArgs e) //逻辑入口
        {
            _stockdata.GetExChangeData();            
        }
        public void ThreadActionMsg(string msg)
        {
            Invoke(new ActionDeleGate(ActionMsg), new object[] { msg });
        }
        public void ActionMsg(string msg)
        {
            if (msg.StartsWith("showexchangingtime-"))
                textBoxExchangeTime.Text = msg.Substring(19);

        }
        private void ThreadShowMsg(string msg)
        {
            this.Invoke(new ShowDeleGate(showfiletxt), new object[] { msg });
        }
        private void ThreadAppendMsg(string msg)
        {
            Invoke(new ShowDeleGate(showappendfiletxt), new object[] { msg });
        }
        private void ThreadCompleteRun()
        {
            Invoke(new CompleteDeleGate(CompleteRun));
        }
        public void CompleteRun()
        {
            ;
        }
        public void showfiletxt(string file)
        {
            this.textBoxInfor.Text = file;
        }
        public void showappendfiletxt(string file)
        {
            this.textBoxInfor.Text += file;
        }
        //private Button completebtn;
        private bool isinitdatarunning;
        private string initdataaction;
    }
}
#region runnet
/*
ublic void RunNet()
        {
            bClosed = false;
            isruning = true;           
            ThreadShowMsg("Begining....");
            while (true)
            {        
                    ThreadShowMsg("刷新时间："+System.DateTime.Now.ToString() + "\r\n");
                    ProcessItem( ); 
                try
                {
                    if (!exchangestatus.InLine || !exchangestatus.ExChanging || !exchangestatus.ExChangDay)
                    {
                        ThreadShowMsg("脱机状态或者非交易时段，退出刷新监视");
                        break;
                    }
                    Thread.Sleep(10000);
                }
                catch (Exception e)
                {
                    MFile.AppendAllText("Exception1.log", e.Message + " " +  ""+ " \r\n");
                    ThreadShowMsg("出现故障，退出刷新监视"+e.Message + " " + "" + " \r\n");
                    break;
                }
                if (bClosed) break;
            }
            isruning = false;
        }
        private void ProcessItem()
        {
            bool HasData = exchangestatus.InLine && exchangestatus.ExChangDay;
            if (exchangestatus.InLine && exchangestatus.ExChangDay )
            ds.InitPriceFromNet(_stocks.stocks);
            bool Change = false;
            foreach (Stock s in _stocks.stocks)
            {
               ////////////////////
               //dosomething
            }
            if (Change)
                ThreadShowSelectStocks();
        } 
//*/
#endregion