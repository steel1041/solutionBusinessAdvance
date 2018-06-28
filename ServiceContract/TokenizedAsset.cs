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
        private static readonly byte[] admin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        [Appcall("59aae873270b0dcddae10d9e3701028a31d82433")]
        public static extern object SDTContract(string method, object[] args);

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;


        public static Object Main(string operation, params object[] args)
        {
            var magicstr = "2018-06-26 15:20";
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(admin);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                var callscript = ExecutionEngine.CallingScriptHash;
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
                if (operation == "init")
                {
                    if(args.Length != 4) return false;
                    string name = (string)args[0];
                    string symbol = (string)args[1];
                    byte decimals = (byte)args[2];
                    byte[] addr = (byte[])args[3];

                    if (!Runtime.CheckWitness(addr))
                        return false;
                    return init(name,symbol,decimals);
                }
                //增发代币，直接方法，风险极高
                if (operation == "increase")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    //判断调用者是否是跳板合约
                    byte[] jumpCallScript = Storage.Get(Storage.CurrentContext, "callScript");
                    if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;
                    return increaseByBu(name,addr, value);
                }

            }
            return true;
        }

        //增发货币
        public static bool increaseByBu(string name,byte[] to, BigInteger value)
        {
            if (to.Length != 20) return false;

            if (value <= 0) return false;

            byte[] token = Storage.Get(Storage.CurrentContext, name);
            if (token.Length == 0) return false;

            transfer(name,null,to, value);

            Tokenized t = Helper.Deserialize(token) as Tokenized;
            t.totalSupply = t.totalSupply+value;

            Storage.Put(Storage.CurrentContext, name, Helper.Serialize(t));
            return true;
        }

        public static bool init(string name,string symbol,byte decimals)
        {
            byte[] token = Storage.Get(Storage.CurrentContext, name);
            if (token.Length != 0) return false;

            Tokenized t = new Tokenized();
            t.decimals = decimals;
            t.name = name;
            t.symbol = symbol;
            t.totalSupply = 0;
            Storage.Put(Storage.CurrentContext, name, Helper.Serialize(t));
            return true;
        }

        public static BigInteger totalSupply(string name)
        {
            byte[] token = Storage.Get(Storage.CurrentContext, name);
            if (token.Length == 0) return 0;
            return (Helper.Deserialize(token) as Tokenized).totalSupply;
        }

        public static string name(string name)
        {
            byte[] token = Storage.Get(Storage.CurrentContext, name);
            if (token.Length == 0) return "";
            return (Helper.Deserialize(token) as Tokenized).name;
        }

        public static string symbol(string name)
        {
            byte[] token = Storage.Get(Storage.CurrentContext, name);
            if (token.Length == 0) return "";
            return (Helper.Deserialize(token) as Tokenized).symbol;
        }

        public static byte decimals(string name)
        {
            byte[] token = Storage.Get(Storage.CurrentContext, name);
            if (token.Length == 0) return 8;
            return (Helper.Deserialize(token) as Tokenized).decimals;
        }

        public static BigInteger balanceOf(string name,byte[] address)
        {
            byte[] token = Storage.Get(Storage.CurrentContext, name);
            if (token.Length == 0 || address.Length != 20) return 0;
            return Storage.Get(Storage.CurrentContext, name.AsByteArray().Concat(address)).AsBigInteger();
        }


        public static bool transfer(string name,byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;

            if (from == to) return true;

            byte[] token = Storage.Get(Storage.CurrentContext, name);
            if (token.Length == 0) return false;

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



        public class Tokenized
        {
            //对应稳定币名称，唯一性
            public string name;

            //总量等价于已经提取的资产
            public BigInteger totalSupply;

            //简称
            public string symbol;

            //小数位数
            public byte decimals;
        }

        public class TransferInfo
        {
            public byte[] from;
            public byte[] to;
            public BigInteger value;
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
