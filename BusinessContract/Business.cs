using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;
using Helper = Neo.SmartContract.Framework.Helper;
using System.ComponentModel;
using Neo.SmartContract.Framework.Services.System;

namespace BusinessContract
{
    public class Business : SmartContract
    {

        //超级管理员账户
        private static readonly byte[] admin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        [Appcall("59aae873270b0dcddae10d9e3701028a31d82433")]
        public static extern object SDTContract(string method, object[] args);

        [Appcall("59aae873270b0dcddae10d9e3701028a31d82433")]
        public static extern object TokenizedContract(string method, object[] args);

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        //因子
        private const ulong factor = 100000000;

        //总计数量
        private const ulong TOTAL_AMOUNT = 0;

        private const string TOTAL_SUPPLY = "totalSupply";

        private const string TOTAL_GENERATE = "totalGenerate";

        private const string CONFIG_KEY = "config_key";

        /*Config 相关*/
        private const string CONFIG_SDT_PRICE = "config_sdt_price";
        private const string CONFIG_SDT_RATE = "config_sdt_rate";

        private const string STORAGE_ACCOUNT = "storage_account";
        private const string STORAGE_TXID = "storage_txid";

        //交易类型
        public enum ConfigTranType
        {
            TRANSACTION_TYPE_OPEN = 1,//建仓
            TRANSACTION_TYPE_RESERVE,//锁仓
            TRANSACTION_TYPE_EXPANDE,//提取
            TRANSACTION_TYPE_FREE,//释放
            TRANSACTION_TYPE_WIPE,//赎回
            TRANSACTION_TYPE_SHUT,//关闭
            TRANSACTION_TYPE_FORCESHUT,//对手关闭
            TRANSACTION_TYPE_GIVE,//转移所有权
        }


        public static Object Main(string operation, params object[] args)
        {
            var magicstr = "2018-06-26 15:20";

            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(admin);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
               
                if (operation == "setAccount")
                {
                    if (args.Length != 1) return false;
                    byte[] address = (byte[])args[0];
                    if (!Runtime.CheckWitness(admin)) return false;
                    return setAccount(address);
                }

                if (operation == "setConfig")
                {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    BigInteger value = (BigInteger)args[1];
                    return setConfig(key, value);
                }

                if (operation == "getConfig")
                {
                    if (args.Length != 1) return false;
                    string key = (string)args[0];
                    return getConfig(key);
                }

                if (operation == "getSAR4B")
                {
                    if (args.Length != 1) return false;
                    string name = (string)args[0];
                    return getSAR4B(name);
                }

                if (operation == "openSAR4B")
                {
                    if (args.Length != 4) return false;
                    string name = (string)args[0];
                    string symbol = (string)args[1];
                    byte decimals = (byte)args[2];
                    byte[] addr = (byte[])args[3];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return openSAR4B(name,symbol,decimals,addr);
                }
                //储备，锁定资产
                if (operation == "reserve")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return reserve(name,addr, value);
                }
                //增加
                if (operation == "expande")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return expande(name,addr,value);
                }
                //关闭
                if (operation == "close")
                {
                    if (args.Length != 2) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return close(name,addr);

                }
               
            }
            return true;
        }


        private static Boolean close(string name,byte[] addr)
        {
            if (addr.Length != 20) return false;

            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length == 0) return false;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) return false;

            byte[] to = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            if (to.Length == 0) return false;

            byte[] owner = info.owner;
            BigInteger locked = info.locked;
            BigInteger hasDrawed = info.hasDrawed;

            //当前余额必须要大于负债
            //BigInteger balance = balanceOf(addr);
            //if (hasDrawed > balance) return false;

            //var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            ////从合约地址转账
            //byte[] from = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            //object[] arg = new object[3];
            //arg[0] = from;
            //arg[1] = owner;
            //arg[2] = locked;

            //if (!(bool)SDTContract("transfer_contract", arg)) return false;

            //if (hasDrawed > 0)
            //{
            //    //先要销毁SD
            //    transfer(addr, null, hasDrawed);
            //    //减去总量
            //    operateTotalSupply(0 - hasDrawed);
            //}
            ////关闭CDP
            //Storage.Delete(Storage.CurrentContext, key);

            ////记录交易详细数据
            //CDPTransferDetail detail = new CDPTransferDetail();
            //detail.from = addr;
            //detail.cdpTxid = cdpInfo.txid;
            //detail.txid = txid;
            //detail.type = (int)ConfigTranType.TRANSACTION_TYPE_SHUT;
            //detail.operated = hasDrawed;
            //detail.hasLocked = locked;
            //detail.hasDrawed = hasDrawed;
            //Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;
        }

        public static bool setAccount(byte[] address)
        {
            byte[] addr = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);

            if (addr.Length != 0) return false;
            Storage.Put(Storage.CurrentContext, STORAGE_ACCOUNT, address);
            return true;
        }

        public static bool setConfig(string key, BigInteger value)
        {
            if (key == null || key == "") return false;
            //只允许超管操作
            if (!Runtime.CheckWitness(admin)) return false;

            Storage.Put(Storage.CurrentContext, key.AsByteArray(), value);

            return true;
        }

        public static BigInteger getConfig(string key)
        {
            if (key == null || key == "") return 0;

            return Storage.Get(Storage.CurrentContext, key.AsByteArray()).AsBigInteger();
        }

        /*查询债仓详情*/
        public static SARInfo getSAR4B(string name)
        {
            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length == 0) return null;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            return info;
        }

        /*开启一个新的债仓*/
        public static bool openSAR4B(string name, string symbol,byte decimals,byte[] addr)
        {
            if (addr.Length != 20) return false;

            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length != 0) return false;

            byte[] txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARInfo info = new SARInfo();
            info.decimals = decimals;
            info.name = name;
            info.hasDrawed = 0;
            info.locked = 0;
            info.owner = addr;
            info.totalSupply = 0;
            info.txid = txid;

            //调用标准合约
            object[] arg = new object[3];
            arg[0] = name;
            arg[1] = symbol;
            arg[2] = decimals;
            //保存标准
            if (!(bool)TokenizedContract("init", arg)) return false;

            //保存SAR
            Storage.Put(Storage.CurrentContext,name,Helper.Serialize(info));

            //交易详细信息
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_OPEN;
            detail.operated = 0;
            detail.hasLocked = 0;
            detail.hasDrawed = 0;

            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;
        }

        /*向债仓锁定数字资产*/
        public static bool reserve(string name,byte[] addr, BigInteger value)
        {
            if (addr.Length != 20) return false;
            if (value == 0) return false;
            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length == 0) return false;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) return false;

            byte[] to = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            if (to.Length == 0) return false;

            object[] arg = new object[3];
            arg[0] = addr;
            arg[1] = to;
            arg[2] = value;

            if (!(bool)SDTContract("transfer", arg)) return false;

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            byte[] used = Storage.Get(Storage.CurrentContext, txid);
            /*判断txid是否已被使用*/
            if (used.Length != 0) return false;


            info.locked = info.locked + value;
            BigInteger currLock = info.locked;
            Storage.Put(Storage.CurrentContext, name, Helper.Serialize(info));

            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_RESERVE;
            detail.operated = value;
            detail.hasLocked = currLock;
            detail.hasDrawed = info.hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));

            /*记录txid 已被使用*/
            Storage.Put(Storage.CurrentContext, txid, addr);
            return true;
        }

        public static bool expande(string name, byte[] addr, BigInteger value)
        {
            if (addr.Length != 20) return false;
            if (value == 0) return false;
            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length == 0) return false;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) return false;

            BigInteger locked = info.locked;
            BigInteger hasDrawed = info.hasDrawed;

            BigInteger sdt_price = getConfig(CONFIG_SDT_PRICE);
            BigInteger sdt_rate = getConfig(CONFIG_SDT_RATE); ;

            BigInteger sdusd_limit = sdt_price * locked * 100 / sdt_rate;

            if (sdusd_limit < hasDrawed + value) return false;

            //调用标准增发
            object[] arg = new object[3];
            arg[0] = name;
            arg[1] = addr;
            arg[2] = value;

            if (!(bool)SDTContract("increase", arg)) return false;

            info.hasDrawed = hasDrawed + value;
            Storage.Put(Storage.CurrentContext, name, Helper.Serialize(info));

            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_EXPANDE;
            detail.operated = value;
            detail.hasLocked = locked;
            detail.hasDrawed = hasDrawed;

            Storage.Put(Storage.CurrentContext, txid, Helper.Serialize(detail));
            return true;
        }


        public class SARInfo
        {
            //对应稳定币名称，唯一性
            public string name;

            //总量等价于已经提取的资产
            public BigInteger totalSupply;

            //简称
            public string symbol;

            //小数位数
            public byte decimals;

            //创建者
            public byte[] owner;

            //交易序号
            public byte[] txid;

            //被锁定的资产,如PNeo
            public BigInteger locked;

            //已经提取的资产，如SDUSDT  
            public BigInteger hasDrawed;
        }

        public class SARTransferInfo
        {
            //拥有者
            public byte[] owner;

            //交易序号
            public byte[] txid;

            //被锁定的资产,如PNeo
            public BigInteger locked;

            //已经提取的资产，如SDUSDT  
            public BigInteger hasDrawed;
        }


        public class TransferInfo
        {
            public byte[] from;
            public byte[] to;
            public BigInteger value;
        }


        public class SARTransferDetail
        {
            //地址
            public byte[] from;

            //SAR交易序号
            public byte[] sarTxid;

            //交易序号
            public byte[] txid;

            //操作对应资产的金额,如PNeo
            public BigInteger operated;

            //已经被锁定的资产金额,如PNeo
            public BigInteger hasLocked;

            //已经提取的资产金额，如SDUSDT  
            public BigInteger hasDrawed;

            //操作类型
            public int type;
        }

        private static byte[] ConvertN(BigInteger n)
        {
            if (n == 0)
                return new byte[2] { 0x00, 0x00 };
            if (n == 1)
                return new byte[2] { 0x00, 0x01 };
            if (n == 2)
                return new byte[2] { 0x00, 0x02 };
            if (n == 3)
                return new byte[2] { 0x00, 0x03 };
            if (n == 4)
                return new byte[2] { 0x00, 0x04 };
            throw new Exception("not support.");
        }
    }
}
