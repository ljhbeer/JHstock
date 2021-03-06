﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using JHStock.Update;
using Newtonsoft.Json;
using Tools;

namespace JHStock
{
    public  class Stocks
    {
        public Stocks(GlobalConfig cfg)
        {
            this.Gcfg = cfg;
            this.db = cfg.db;
            _stocks = null;
            _indexstocks = null;
            _numcodestocks = null;
            _indexdata = null;
            _macddata = null;
            _listdata = null;
            //add new
            _cativeprices =null;
            _netszdate = null;
        }
        public List<Stock> stocks { 
            get
            {
                InitStocks();
                return _stocks;
            }
        }
        public List<int> ListDate
        {
            get
            {
                if (_listdata == null)
                    _listdata = CKData.GetKDataDate(Gcfg.Baseconfig.shpath + "\\sh999999.day");
                return _listdata;
            }
        }       
        public GlobalConfig Gcfg { get; set; }
        public float[,] MacdData { get { InitMacdDay(); return _macddata; } }
        public int[,] IndexData { get { InitIndexDay(); return _indexdata; } }

        public void ResetMacdData()
        {
            _macddata = null;
            _macdheaddata = null;
        }
        public void ReloadListDate()
        {
            _listdata = null;
        }
        public Stock StockByIndex(int index)
        {
            if (_indexstocks == null)
                InitStocks();
            if (index > 0 && index < 2048)
                return _indexstocks[index];
            return null;
        }
        public Stock StockByNumCode(string NumCode)
        {
            if (_numcodestocks == null)
                InitStocks();
            if (_numcodestocks.ContainsKey(NumCode))
                return _numcodestocks[NumCode];
            return null;
        }
        public int GetStartDatePos(int  day)
        {
            InitStocks();
            InitIndexDay();
            for (int i = 0; i <_indexdata.GetLength(0); i++)
            {
                if (_indexdata[i, 0] >= day)
                    return i;
            }
            return -1;
        }

        private void InitStocks()
        {
            if (_stocks == null)
            {
                _stocks = new List<Stock>();
                string sql = "select * from stockcode order by ID";
                DataSet ds = db.query(sql);
                foreach (DataRow dr in ds.Tables[0].Rows)
                {
                    Stock s = new Stock((int)dr["ID"], (string)dr["scode"], (string)dr["sname"],Gcfg);
                    _stocks.Add(s);
                }
                //Init _indexstocks
                _indexstocks = new Stock[2048];
                for (int i = 0; i < 2048; i++)
                    _indexstocks[i] = null;
                foreach (Stock s in _stocks)
                    _indexstocks[s.ID] = s;
                //Init _Numstocks
                _numcodestocks = new Dictionary<string, Stock>();
                foreach (Stock s in _stocks)
                    _numcodestocks.Add(s.NumCode, s);
            }
        }
        private void InitIndexDay()
        {
            if (_indexdata == null)
            {
                LoadAllData();
            }
        }
        private void InitMacdDay()
        {
            if (_macddata == null)
            {
                LoadAllMacdData();
            }
        }
        private void LoadAllMacdData(string allstockfile = "", int StructSize = 2048*4)
        {
            if (allstockfile == "")
                allstockfile = Gcfg.Baseconfig.IndexPath().Replace("index", "macd");
            if (File.Exists(allstockfile))
            {
                byte[] buffer = File.ReadAllBytes(allstockfile);
                int intsize = sizeof(int);
                int itemintsize = StructSize / intsize;
                _macdheaddata = new int[itemintsize];
                int headsize = itemintsize *intsize;
                int itemcount = (int)((buffer.Length-headsize) / StructSize);
                _macddata = new float[itemintsize, itemcount];
                unsafe
                {
                    IntPtr bytePtr = Marshal.AllocHGlobal(intsize);
                    ///////////////////////Copy Head
                    for (int i = 0; i < _macdheaddata.Length; i++)
                    {
                        Marshal.Copy(buffer, i * intsize, bytePtr, intsize);
                        _macdheaddata[i ] = *((int*)bytePtr);
                    }

                    ////////////////////////copy content
                    for (int i = 0; i < _macddata.Length; i++)
                    {
                        Marshal.Copy(buffer, i * intsize+headsize, bytePtr, intsize);
                        _macddata[i / itemcount, i % itemcount] = *((float*)bytePtr);
                    }
                    Marshal.FreeHGlobal(bytePtr);
                }
            }
        }
        private void LoadAllData(string allstockfile = "", int StructSize = 2048*4)
        {
            if (allstockfile == "")
                allstockfile = Gcfg.Baseconfig.IndexPath();
            if (File.Exists(allstockfile))
            {
                byte[] buffer = File.ReadAllBytes(allstockfile);
                int intsize = sizeof(int);
                int itemintsize = StructSize / intsize;
                int itemcount = (int)(buffer.Length / StructSize);
                _indexdata = new int[itemcount, itemintsize];
                unsafe
                {
                    IntPtr bytePtr = Marshal.AllocHGlobal(intsize);
                    for (int i = 0; i < _indexdata.Length; i++)
                    {
                        Marshal.Copy(buffer, i * intsize, bytePtr, intsize);
                        _indexdata[i / itemintsize, i % itemintsize] = *((int*)bytePtr);
                    }
                    Marshal.FreeHGlobal(bytePtr);
                }
            }
        }    

        private Dictionary<string, Stock> _numcodestocks;
        private List<int> _listdata;
        private Stock[] _indexstocks;
        private int[,] _indexdata;
        private int[] _macdheaddata;
        private float[,] _macddata;  
        private List<Stock> _stocks;
        private Db.ConnDb db;
        
        //AddNew
        public CActivePrices CActiveprices{
        	get{
        		if(_cativeprices==null ){
        			InitStocks();
        			if(_stocks!=null){
        				_cativeprices = new CActivePrices(this);
        			}
        		}
        		return _cativeprices;
        	}
        }
        private CActivePrices _cativeprices;
        public List<int> ShNetDate{
        	get{
        		if(_netszdate==null){
					string url = @"http://web.ifzq.gtimg.cn/appstock/app/fqkline/get?_var=kline_dayqfq&param=sh000001,day,,,254,qfq";
					string txt = CWeb.GetWebClient(url).Substring(13);
					QQStocks qs = JsonConvert.DeserializeObject<QQStocks>(txt);
					_netszdate=qs.data["sh000001"].day.Select(
						r => Convert.ToInt32( r[0].Replace("-",""))).ToList();
        		}
        		return _netszdate;
        	}
        }
        private List<int> _netszdate;
    }
}
