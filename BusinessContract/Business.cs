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
        /*存储结构有     
        * map(address,balance)   存储SAR信息    key = 0x12+address
        * map(txid,TransferInfo) 存储交易详情   key = 0x13+txid
        * map(str,address)       存储配置信息   key = 0x14+str
       */
        //管理员账户
        private static readonly byte[] admin = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        public delegate object NEP5Contract(string method, object[] args);

        /* name,addr,sartxid,txid,type,operated*/
        [DisplayName("sarOperator")]
        public static event Action<byte[],byte[], byte[], byte[], BigInteger,BigInteger> Operated;

        /*Config 相关*/
        private const string CONFIG_SDT_PRICE = "sdt_price";

        //最低抵押率
        private const string CONFIG_SDT_RATE = "config_sdt_rate";

        //最低抵押率
        private const string CONFIG_LIMIT_RATE = "config_limit_rate";

        private const string STORAGE_ACCOUNT = "storage_account";

        private const string SERVICE_FEE = "service_fee";

        private const ulong FOURTEEN_POWER = 100000000000000;

        //交易类型
        public enum ConfigTranType
        {
            TRANSACTION_TYPE_OPEN = 1,//建仓
            TRANSACTION_TYPE_INIT,    //初始化代币
            TRANSACTION_TYPE_RESERVE,//锁仓
            TRANSACTION_TYPE_EXPANDE,//提取
            TRANSACTION_TYPE_WITHDRAW,//释放
            TRANSACTION_TYPE_CONTRACT,//赎回
            TRANSACTION_TYPE_SHUT,//关闭
            TRANSACTION_TYPE_REDEEM//用户 赎回

        }

        //SAR状态
        public enum ConfigSARStatus
        {
            SAR_STATUS_SAFETY = 1,  //安全
            SAR_STATUS_RISK,        //危险
            SAR_STATUS_CLOSE,       //不可用
        }


        public static Object Main(string operation, params object[] args)
        {
            var magicstr = "2018-07-25";

            if (Runtime.Trigger == TriggerType.Verification)
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "getSAR4B")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];
                    return getSAR4B(addr);
                }
                if (operation == "openSAR4B")
                {
                    if (args.Length != 6) return false;
                    string name = (string)args[0];
                    string symbol = (string)args[1];
                    byte decimals = (byte)args[2];
                    byte[] addr = (byte[])args[3];
                    string anchor = (string)args[4];
                    byte[] tokenizedAssetID = (byte[])args[5];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return openSAR4B(name,symbol,decimals,addr,anchor,tokenizedAssetID);
                }
                if (operation == "initToken")
                {
                    if (args.Length != 3) return false;
                    byte[] addr = (byte[])args[0];
                    byte[] sdsAssetID = (byte[])args[1];
                    byte[] tokenizedAssetID = (byte[])args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return initToken(addr,sdsAssetID,tokenizedAssetID);
                }
                //储备，锁定资产
                if (operation == "reserve")
                {
                    if (args.Length != 4) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    byte[] sdsAssetID = (byte[])args[3];

                    if (!Runtime.CheckWitness(addr)) return false;

                    return reserve(name,addr, value, sdsAssetID);
                }
                //增加
                if (operation == "expande")
                {
                    if (args.Length != 5) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    byte[] oracleAssetID = (byte[])args[3];
                    byte[] tokenizedAssetID = (byte[])args[4];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return expande(name,addr,value, oracleAssetID, tokenizedAssetID);
                }
                //擦除
                if (operation == "contract")
                {
                    if (args.Length != 4) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    byte[] tokenizedAssetID = (byte[])args[3];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return contract(name, addr, value,tokenizedAssetID);
                }
                //提现
                if (operation == "withdraw")
                {
                    if (args.Length != 6) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    byte[] oracleAssetID = (byte[])args[3];
                    byte[] sdsAssetID = (byte[])args[4];
                    byte[] tokenizedAssetID = (byte[])args[5];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return withdraw(name, addr, value, oracleAssetID, sdsAssetID, tokenizedAssetID);
                }
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
                //个人用户赎回不安全的仓位
                if (operation == "redeem")
                {
                    if (args.Length != 5) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    byte[] sdsAssetID = (byte[])args[2];
                    byte[] oracleAssetID = (byte[])args[3];
                    byte[] tokenizedAssetID = (byte[])args[4];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return redeem(name,addr, sdsAssetID, oracleAssetID, tokenizedAssetID);

                }
                if (operation == "getConfigTest")
                {
                    if (args.Length != 1) return false;
                    string key = (string)args[0];
                    return getConfig(key);
                }
                //修改SAR状态为关闭
                if (operation == "settingSAR")
                {
                    if (args.Length != 2) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];

                    if (!Runtime.CheckWitness(admin)) return false;
                    return settingSAR(name, addr);
                }
                #region 升级合约,耗费490,仅限管理员
                if (operation == "upgrade")
                {
                    //不是管理员 不能操作
                    if (!Runtime.CheckWitness(admin))
                        return false;

                    if (args.Length != 1 && args.Length != 9)
                        return false;

                    byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
                    byte[] new_script = (byte[])args[0];
                    //如果传入的脚本一样 不继续操作
                    if (script == new_script)
                        return false;

                    byte[] parameter_list = new byte[] { 0x07, 0x10 };
                    byte return_type = 0x05;
                    //1|1|4
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

        public static bool setAccount(byte[] address)
        {
            //byte[] addr = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            if (address.Length != 20) return false;
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
        public static SARInfo getSAR4B(byte[] addr)
        {
            var key = new byte[] { 0x12 }.Concat(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) return null;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            return info;
        }

        public static bool settingSAR(string name,byte[] addr)
        {
            if (addr.Length != 20) return false;
            var key = new byte[] { 0x12 }.Concat(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, name);
            if (sar.Length == 0) return false;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) return false;

            info.status = (int)ConfigSARStatus.SAR_STATUS_CLOSE;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));
            return true;
        }

        /*提现抵押资产*/
        public static bool withdraw(string name, byte[] addr, BigInteger value, byte[] oracleAssetID, byte[] sdsAssetID,byte[] tokenizedAssetID)
        {
            if (addr.Length != 20) return false;
            if (value == 0) return false;

            var key = new byte[] { 0x12 }.Concat(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) return false;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) return false;

            //SAR状态
            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE) return false;

            byte[] to = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            if (to.Length == 0) return false;

            BigInteger locked = info.locked;
            BigInteger hasDrawed = info.hasDrawed;

            //调用Oracle,查询SDT价格，如：8$=价格*100000000
            object[] arg = new object[1];
            arg[0] = CONFIG_SDT_PRICE;

            var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

            BigInteger sdt_price = (BigInteger)OracleContract("getPrice", arg);

            //调用Oracle,查询锚定价格，如：100$=价格*100000000
            arg = new object[1];
            arg[0] = info.anchor;
            BigInteger anchor_price = (BigInteger)OracleContract("getAnchorPrice", arg);

            //当前兑换率，需要从配置中心获取
            BigInteger rate = getConfig(CONFIG_SDT_RATE);

            //计算已经兑换过的SDT量
            BigInteger hasDrawSDT = hasDrawed * rate * FOURTEEN_POWER / (sdt_price * anchor_price);

            //释放的总量大于已经剩余，不能操作
            if (value > locked - hasDrawSDT) return false;

            byte[] from = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            if (from.Length == 0) return false;

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            object[] param = new object[3];
            param[0] = from;
            param[1] = addr;
            param[2] = value;

            var SDSContract = (NEP5Contract)sdsAssetID.ToDelegate();

            if (!(bool)SDSContract("transfer_contract", param)) return false;

            //重新设置锁定量
            info.locked = locked - value;

            //提现为0了，需要关闭token
            if (info.locked == 0)
            {
                param = new object[2];
                param[0] = name;
                param[1] = addr;

                var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
                if (!(bool)TokenizedContract("close", param)) return false;
            }

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_WITHDRAW;
            detail.operated = value;
            detail.hasLocked = locked - value;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, new byte[] { 0x13 }.Concat(txid), Helper.Serialize(detail));
            return true;
        }


        /*返还部分仓位*/
        public static bool contract(string name, byte[] addr, BigInteger value, byte[] tokenizedAssetID)
        {
            if (addr.Length != 20) return false;
            if (value == 0) return false;
            var key = new byte[] { 0x12 }.Concat(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) return false;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) return false;

            //SAR状态
            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE) return false;

            byte[] to = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            if (to.Length == 0) return false;

            BigInteger hasDrawed = info.hasDrawed;
            //不能超过已经获取
            if (value > hasDrawed) return false;

            object[] arg = new object[3];
            arg[0] = name;
            arg[1] = addr;
            arg[2] = value;

            var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();

            if (!(bool)TokenizedContract("destory", arg)) return false;

            info.hasDrawed = hasDrawed - value;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_CONTRACT;
            detail.operated = value;
            detail.hasLocked = info.locked;
            detail.hasDrawed = info.hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, new byte[] { 0x13 }.Concat(txid), Helper.Serialize(detail));
            return true;
        }

        /*开启一个新的债仓*/
        public static bool openSAR4B(string name, string symbol,byte decimals,byte[] addr,string anchor, byte[] tokenizedAssetID)
        {
            if (addr.Length != 20) return false;

            //判断该地址是否拥有SAR
            var key = new byte[] { 0x12 }.Concat(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length != 0) return false;

            //调用标准合约
            object[] arg = new object[1];
            arg[0] = name;

            //验证name
            var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
            string str = (string)TokenizedContract("name", arg);

            if (str.Length > 0) return false;

            byte[] txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARInfo info = new SARInfo();
            info.symbol = symbol;
            info.decimals = 8;
            info.name = name;
            info.hasDrawed = 0;
            info.locked = 0;
            info.owner = addr;
            info.txid = txid;
            info.status = 1;
            info.anchor = anchor;

            //保存SAR
            Storage.Put(Storage.CurrentContext,key,Helper.Serialize(info));

            //交易详细信息
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_OPEN;
            detail.operated = 0;
            detail.hasLocked = 0;
            detail.hasDrawed = 0;

            Storage.Put(Storage.CurrentContext, new byte[] { 0x13 }.Concat(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(name.AsByteArray(),addr,txid,txid, (int)ConfigTranType.TRANSACTION_TYPE_OPEN, 0);
            return true;
        }

        public static bool initToken(byte[] addr, byte[] sdsAssetID, byte[] tokenizedAssetID)
        {
            //判断该地址是否拥有SAR
            var key = new byte[] { 0x12 }.Concat(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) return false;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);

            //验证name
            object[] arg = new object[1];
            arg[0] = info.name;

            var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
            string str = (string)TokenizedContract("name", arg);

            if (str.Length > 0) return false;

            byte[] to = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            if (to.Length == 0) return false;

            BigInteger serviceFree = Storage.Get(Storage.CurrentContext, SERVICE_FEE).AsBigInteger();
            //默认10sdt
            if (serviceFree == 0) serviceFree = 1000000000;
            //转入抵押手续费
            arg = new object[3];
            arg[0] = addr;
            arg[1] = to;
            arg[2] = serviceFree;

            var SDSContract = (NEP5Contract)sdsAssetID.ToDelegate();
            if (!(bool)SDSContract("transfer", arg)) return false;

            info.locked = info.locked + serviceFree;

            arg = new object[4];
            arg[0] = info.name;
            arg[1] = info.symbol;
            arg[2] = info.decimals;
            arg[3] = addr;
            
            //保存标准
            if (!(bool)TokenizedContract("init", arg)) return false;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //交易详细信息
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_INIT;
            detail.operated = 0;
            detail.hasLocked = 0;
            detail.hasDrawed = 0;

            Storage.Put(Storage.CurrentContext, new byte[] { 0x13 }.Concat(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(info.name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_INIT, 0);
            return true;
        }

        /*向债仓锁定数字资产*/
        public static bool reserve(string name,byte[] addr, BigInteger value, byte[] sdsAssetID)
        {
            if (addr.Length != 20) return false;
            if (value == 0) return false;
            var key = new byte[] { 0x12 }.Concat(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) return false;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) return false;

            //SAR状态
            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE) return false;

            byte[] to = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            if (to.Length == 0) return false;

            object[] arg = new object[3];
            arg[0] = addr;
            arg[1] = to;
            arg[2] = value;

            var SDSContract = (NEP5Contract)sdsAssetID.ToDelegate();

            if (!(bool)SDSContract("transfer", arg)) return false;

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            info.locked = info.locked + value;
            BigInteger currLock = info.locked;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_RESERVE;
            detail.operated = value;
            detail.hasLocked = currLock;
            detail.hasDrawed = info.hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, new byte[] { 0x13 }.Concat(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(info.name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_RESERVE, value);
            return true;
        }

        public static bool expande(string name, byte[] addr, BigInteger value, byte[] oracleAssetID, byte[] tokenizedAssetID)
        {
            if (addr.Length != 20) return false;
            if (value == 0) return false;

            var key = new byte[] { 0x12 }.Concat(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) return false;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) return false;

            //SAR状态
            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE) return false;

            BigInteger locked = info.locked;
            BigInteger hasDrawed = info.hasDrawed;

            //调用Oracle,查询SDT价格，如：8$=价格*100000000
            object[] arg = new object[1];
            arg[0] = CONFIG_SDT_PRICE;

            var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

            BigInteger sdt_price =  (BigInteger)OracleContract("getPrice",arg);

            //调用Oracle,查询锚定价格，如：100$=价格*100000000
            arg = new object[1];
            arg[0] = info.anchor;
            BigInteger anchor_price = (BigInteger)OracleContract("getAnchorPrice", arg);

            BigInteger sdt_rate = getConfig(CONFIG_SDT_RATE); ;

            BigInteger sdusd_limit = sdt_price * anchor_price * locked / (sdt_rate * FOURTEEN_POWER);

            if (sdusd_limit < hasDrawed + value) return false;

            //调用标准增发
            arg = new object[3];
            arg[0] = name;
            arg[1] = addr;
            arg[2] = value;

            var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
            if (!(bool)TokenizedContract("increase", arg)) return false;

            info.hasDrawed = hasDrawed + value;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

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

            Storage.Put(Storage.CurrentContext, new byte[] { 0x13 }.Concat(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(info.name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_EXPANDE, value);
            return true;
        }

        public static bool redeem(string name, byte[] addr, byte[] sdsAssetID, byte[] oracleAssetID, byte[] tokenizedAssetID)
        {
            if (addr.Length != 20) return false;

            var key = new byte[] { 0x12 }.Concat(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) return false;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);

            //SAR状态,必须是关闭状态
            if (info.status != (int)ConfigSARStatus.SAR_STATUS_CLOSE) return false;
            //转账给用户SDT
            byte[] from = Storage.Get(Storage.CurrentContext, STORAGE_ACCOUNT);
            if (from.Length == 0) return false;

            //调用标准增发
            object[] arg = new object[2];
            arg[0] = name;
            arg[1] = addr;

            var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
            BigInteger balance =  (BigInteger)TokenizedContract("balanceOf",arg);
            if (balance <= 0) return false;

            //调用Oracle,查询SDT价格，如：8$=价格*100000000
            arg = new object[1];
            arg[0] = CONFIG_SDT_PRICE;

            var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

            BigInteger sdt_price = (BigInteger)OracleContract("getPrice", arg);

            //调用Oracle,查询锚定价格，如：100$=价格*100000000
            arg = new object[1];
            arg[0] = info.anchor;
            BigInteger anchor_price = (BigInteger)OracleContract("getAnchorPrice", arg);

            BigInteger sdt_rate = getConfig(CONFIG_SDT_RATE); ;
            //计算可赎回的SDT
            BigInteger redeem = (balance * sdt_rate * FOURTEEN_POWER) / (info.locked * sdt_price * anchor_price);

            //销毁用户的稳定代币
            arg = new object[3];
            arg[0] = name;
            arg[1] = addr;
            arg[2] = balance;
            if (!(bool)TokenizedContract("destory", arg)) return false;

            object[] param = new object[3];
            param[0] = from;
            param[1] = addr;
            param[2] = redeem;

            var SDSContract = (NEP5Contract)sdsAssetID.ToDelegate();

            if (!(bool)SDSContract("transfer_contract", param)) return false;

            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_REDEEM;
            detail.operated = redeem;
            detail.hasLocked = info.locked;
            detail.hasDrawed = info.hasDrawed;

            Storage.Put(Storage.CurrentContext, new byte[] { 0x13 }.Concat(txid), Helper.Serialize(detail));
            //触发操作事件
            Operated(info.name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_REDEEM, redeem);
            return true;
        }


        public class SARInfo
        {
            //对应稳定币名称，唯一性
            public string name;

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

            //1安全  2不安全 3不可用   
            public int status;

            //锚定物类型
            public string anchor;
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
