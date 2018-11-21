using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.Numerics;
using System.ComponentModel;


namespace ServiceContract
{
    public class TokenizedAsset : SmartContract
    {

        /*存储结构有     
         * map(address,balance)   存储地址余额   key = 0x11+name+address
         * map(name,token)        存储多token值  key = 0x12+name
         * map(txid,TransferInfo) 存储交易详情   key = 0x13+txid
         * map(str,address)       存储配置信息   key = 0x14+str
        */

        //管理员账户
        private static readonly byte[] admin = Helper.ToScriptHash("AaBmSJ4Beeg2AeKczpXk89DnmVrPn3SHkU");

        //Call合约账户
        private const string CALL_ACCOUNT = "call_script";

        //admin账户
        private const string ADMIN_ACCOUNT = "admin_account";

        [DisplayName("sarTransfer")]
        public static event Action<byte[], byte[], byte[], BigInteger> Transferred;

        private static byte[] getAccountKey(byte[] key) => new byte[] { 0x15 }.Concat(key);

        private static byte[] getNameKey(byte[] name) => new byte[] { 0x12 }.Concat(name);

        private static byte[] getTxidKey(byte[] txid) => new byte[] { 0x13 }.Concat(txid);

        private static byte[] getBalanceKey(byte[] name,byte[] addr) => new byte[] { 0x11 }.Concat(name).Concat(addr);

        public static Object Main(string operation, params object[] args)
        {
            var magicstr = "2018-10-18";

            if (Runtime.Trigger == TriggerType.Verification)
            {
                return false;
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
                    return balanceOf(name, account);
                }
                if (operation == "transfer")
                {
                    if (args.Length != 4) return false;
                    string name = (string)args[0];
                    byte[] from = (byte[])args[1];
                    byte[] to = (byte[])args[2];
                    BigInteger amount = (BigInteger)args[3];

                    if (from.Length != 20 || to.Length != 20)
                        throw new InvalidOperationException("The parameters from and to SHOULD be 20-byte addresses.");

                    if (amount <= 0)
                        throw new InvalidOperationException("The parameter amount MUST be greater than 0.");

                    if (!Runtime.CheckWitness(from) && from.AsBigInteger() != callscript.AsBigInteger())
                        return false;

                    return transfer(name, from, to, amount);
                }
                if (operation == "init")
                {
                    if (args.Length != 4) return false;
                    string name = (string)args[0];
                    string symbol = (string)args[1];
                    byte decimals = (byte)args[2];
                    byte[] addr = (byte[])args[3];

                    if (!Runtime.CheckWitness(addr))
                        return false;
                    //判断调用者是否是跳板合约
                    byte[] jumpCallScript = Storage.Get(Storage.CurrentContext, getAccountKey(CALL_ACCOUNT.AsByteArray()));
                    if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;
                    return init(name, symbol, decimals);
                }
                if (operation == "close")
                {
                    if (args.Length != 2) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];

                    if (!Runtime.CheckWitness(addr))
                        return false;
                    //判断调用者是否是跳板合约
                    byte[] jumpCallScript = Storage.Get(Storage.CurrentContext, getAccountKey(CALL_ACCOUNT.AsByteArray()));
                    if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;
                    return close(name, addr);
                }
                //增发代币
                if (operation == "increase")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    //判断调用者是否是跳板合约
                    byte[] jumpCallScript = Storage.Get(Storage.CurrentContext, getAccountKey(CALL_ACCOUNT.AsByteArray()));
                    if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;
                    return increaseByBu(name, addr, value);
                }
                //销毁代币
                if (operation == "destory")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    //判断调用者是否是跳板合约
                    byte[] jumpCallScript = Storage.Get(Storage.CurrentContext, getAccountKey(CALL_ACCOUNT.AsByteArray()));
                    if (callscript.AsBigInteger() != jumpCallScript.AsBigInteger()) return false;
                    return destoryByBu(name, addr, value);
                }
                //设置跳板调用合约地址
                if (operation == "setAccount")
                {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    byte[] address = (byte[])args[1];

                    if (!checkAdmin()) return false;
                    return setAccount(key, address);
                }
                #region 升级合约,耗费490,仅限管理员
                if (operation == "upgrade")
                {
                    //不是管理员 不能操作
                    if (!checkAdmin()) return false;

                    if (args.Length != 1 && args.Length != 9)
                        return false;

                    byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
                    byte[] new_script = (byte[])args[0];
                    //如果传入的脚本一样 不继续操作
                    if (script == new_script)
                        return false;

                    byte[] parameter_list = new byte[] { 0x07, 0x10 };
                    byte return_type = 0x05;
                    //1|0|4
                    bool need_storage = (bool)(object)05;
                    string name = "business";
                    string version = "1";
                    string author = "alchemint";
                    string email = "0";
                    string description = "alchemint";

                    if (args.Length == 9)
                    {
                        parameter_list = (byte[])args[1];
                        return_type = (byte)args[2];
                        need_storage = (bool)args[3];
                        name = (string)args[4];
                        version = (string)args[5];
                        author = (string)args[6];
                        email = (string)args[7];
                        description = (string)args[8];
                    }
                    Contract.Migrate(new_script, parameter_list, return_type, need_storage, name, version, author, email, description);
                    return true;
                }
                #endregion

            }
            return true;
        }

        private static bool checkAdmin()
        {
            byte[] currAdmin = Storage.Get(Storage.CurrentContext, getAccountKey(ADMIN_ACCOUNT.AsByteArray()));
            if (currAdmin.Length > 0)
            {
                //当前地址和配置地址必须一致
                if (!Runtime.CheckWitness(currAdmin)) return false;
            }
            else
            {
                if (!Runtime.CheckWitness(admin)) return false;
            }
            return true;
        }

        private static bool setAccount(string key, byte[] addr)
        {
            Storage.Put(Storage.CurrentContext, getAccountKey(key.AsByteArray()), addr);
            return true;
        }

        public static bool close(string name, byte[] addr)
        {
            var key = getNameKey(name.AsByteArray());
            byte[] token = Storage.Get(Storage.CurrentContext, key);
            if (token.Length == 0) return false;

            Storage.Delete(Storage.CurrentContext, key);
            return true;
        }


        //增发货币
        public static bool increaseByBu(string name, byte[] to, BigInteger value)
        {
            if (to.Length != 20) return false;

            if (value <= 0) return false;

            var key = getNameKey(name.AsByteArray());
            byte[] token = Storage.Get(Storage.CurrentContext, key);
            if (token.Length == 0) return false;

            if (!transfer(name, null, to, value))
                throw new InvalidOperationException("Operation is error.");

            Tokenized t = Helper.Deserialize(token) as Tokenized;
            t.totalSupply = t.totalSupply + value;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(t));
            return true;
        }

        //销毁货币
        public static bool destoryByBu(string name, byte[] from, BigInteger value)
        {
            if (from.Length != 20) return false;

            if (value <= 0) return false;

            var key = getNameKey(name.AsByteArray());
            byte[] token = Storage.Get(Storage.CurrentContext, key);
            if (token.Length == 0) return false;

            if(!transfer(name, from, null, value))
                throw new InvalidOperationException("Operation is error.");

            Tokenized t = Helper.Deserialize(token) as Tokenized;
            t.totalSupply = t.totalSupply - value;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(t));
            return true;
        }

        public static bool init(string name, string symbol, byte decimals)
        {
            var key = getNameKey(name.AsByteArray());
            byte[] token = Storage.Get(Storage.CurrentContext, key);
            if (token.Length != 0) return false;

            Tokenized t = new Tokenized();
            t.decimals = decimals;
            t.name = name;
            t.symbol = symbol;
            t.totalSupply = 0;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(t));
            return true;
        }

        public static BigInteger totalSupply(string name)
        {
            var key = getNameKey(name.AsByteArray());
            byte[] token = Storage.Get(Storage.CurrentContext, key);
            if (token.Length == 0) return 0;
            return (Helper.Deserialize(token) as Tokenized).totalSupply;
        }

        public static string name(string name)
        {
            var key = getNameKey(name.AsByteArray());
            byte[] token = Storage.Get(Storage.CurrentContext, key);
            if (token.Length == 0) return "";
            return (Helper.Deserialize(token) as Tokenized).name;
        }

        public static string symbol(string name)
        {
            var key = getNameKey(name.AsByteArray());
            byte[] token = Storage.Get(Storage.CurrentContext, key);
            if (token.Length == 0) return "";
            return (Helper.Deserialize(token) as Tokenized).symbol;
        }

        public static byte decimals(string name)
        {
            var key = getNameKey(name.AsByteArray());
            byte[] token = Storage.Get(Storage.CurrentContext, key);
            if (token.Length == 0) return 8;
            return (Helper.Deserialize(token) as Tokenized).decimals;
        }

        public static BigInteger balanceOf(string name, byte[] address)
        {
            var key = getNameKey(name.AsByteArray());
            byte[] token = Storage.Get(Storage.CurrentContext, key);
            if (token.Length == 0 || address.Length != 20) return 0;

            var balanceKey = getBalanceKey(name.AsByteArray(),address);
            return Storage.Get(Storage.CurrentContext, balanceKey).AsBigInteger();
        }


        public static bool transfer(string name, byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;

            if (from == to) return true;

            var key = getNameKey(name.AsByteArray());

            byte[] token = Storage.Get(Storage.CurrentContext, key);
            if (token.Length == 0) return false;

            byte[] fromKey = getBalanceKey(name.AsByteArray(),from);
            byte[] toKey = getBalanceKey(name.AsByteArray(),to);
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

            //notify,这里from,to无需加前缀
            Transferred(name.AsByteArray(), from, to, value);
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

    }
}
