using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;
using Helper = Neo.SmartContract.Framework.Helper;
using System.ComponentModel;
using Neo.SmartContract.Framework.Services.System;

namespace ServiceContract
{
    public class TokenizedAsset : SmartContract
    {

        //超级管理员账户
        private static readonly byte[] SuperAdmin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        [Appcall("59aae873270b0dcddae10d9e3701028a31d82433")]
        public static extern object SDTContract(string method, object[] args);

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
            TRANSACTION_TYPE_LOCK = 1,//锁仓
            TRANSACTION_TYPE_DRAW,//提取
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
                return Runtime.CheckWitness(SuperAdmin);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {

                if (operation == "totalSupply")
                {
                    if (args.Length != 1) return 0;
                    string name = (string)args[0];
                    return totalSupply(name);
                }
                if (operation == "name")
                {
                    if (args.Length != 1) return 0;
                    string n = (string)args[0];
                    return name(n);
                }
                if (operation == "symbol")
                {
                    if (args.Length != 1) return 0;
                    string name = (string)args[0];
                    return symbol(name);
                }
                if (operation == "decimals")
                {
                    if (args.Length != 1) return 0;
                    string name = (string)args[0];
                    return decimals(name);
                }
                if (operation == "balanceOf")
                {
                    if (args.Length != 2) return 0;
                    string name = (string)args[0];
                    byte[] account = (byte[])args[1];
                    return balanceOf(name,account);
                }
                if (operation == "transfer")
                {
                    if (args.Length != 4) return false;
                    string name = (string)args[0];
                    byte[] from = (byte[])args[1];
                    byte[] to = (byte[])args[2];
                    BigInteger value = (BigInteger)args[3];
                    //没有from签名，不让转
                    if (!Runtime.CheckWitness(from))
                        return false;

                    return transfer(name,from, to, value);
                }

            }
            return true;
        }

        public static BigInteger totalSupply(string name)
        {
            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length == 0) return 0;
            return (Helper.Deserialize(sar) as SARInfo).totalSupply;
        }

        public static string name(string name)
        {
            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length == 0) return "";
            return (Helper.Deserialize(sar) as SARInfo).name;
        }

        public static string symbol(string name)
        {
            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length == 0) return "";
            return (Helper.Deserialize(sar) as SARInfo).symbol;
        }

        public static byte decimals(string name)
        {
            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length == 0) return 8;
            return (Helper.Deserialize(sar) as SARInfo).decimals;
        }

        public static BigInteger balanceOf(string name,byte[] address)
        {
            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length == 0 || address.Length != 20) return 0;
            return Storage.Get(Storage.CurrentContext, name.AsByteArray().Concat(address)).AsBigInteger();
        }


        public static bool transfer(string name,byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;

            if (from == to) return true;

            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length == 0) return false;

            SARInfo sarInfo = Helper.Deserialize(sar) as SARInfo;

            byte[] fromKey = name.AsByteArray().Concat(from);
            byte[] toKey = name.AsByteArray().Concat(to);
            //付款方
            if (from.Length > 0)
            {
                BigInteger from_value = Storage.Get(Storage.CurrentContext, fromKey).AsBigInteger();
                if (from_value < value) return false;
                if (from_value == value)
                    Storage.Delete(Storage.CurrentContext, fromKey);
                else
                    Storage.Put(Storage.CurrentContext, fromKey, from_value - value);
            }
            //收款方
            if (to.Length > 0)
            {
                BigInteger to_value = Storage.Get(Storage.CurrentContext, toKey).AsBigInteger();
                Storage.Put(Storage.CurrentContext, toKey, to_value + value);
            }
            //记录交易信息
            //setTxInfo(from, to, value);
            //notify
            Transferred(fromKey, toKey, value);
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

            //CDP交易序号
            public byte[] cdpTxid;

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

        public static bool operateTotalSupply(BigInteger mount)
        {
            BigInteger current = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY).AsBigInteger();
            if (current + mount >= 0)
            {
                Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY, current + mount);
            }
            return true;
        }

    }
}
