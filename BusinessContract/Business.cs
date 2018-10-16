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
        public static event Action<byte[], byte[], byte[], byte[], BigInteger, BigInteger> Operated;

        /* name,addr,sartxid,anchor,symbol,decimals*/
        [DisplayName("nep55Operator")]
        public static event Action<byte[], byte[], byte[], byte[], byte[], BigInteger> Nep55Operated;

        /*Config 相关*/
        private const string CONFIG_SDS_PRICE = "sds_price";

        //最低抵押率
        //private const string CONFIG_SDS_RATE = "config_sds_rate";
        private const string CONFIG_LIQUIDATION_RATE_B = "liquidate_line_rate_b";

        //合约收款账户
        private const string STORAGE_ACCOUNT = "storage_account";

        //新合约收款账户
        private const string STORAGE_ACCOUNT_NEW = "storage_account_new";

        //SDS合约账户
        private const string SDS_ACCOUNT = "sds_account";

        //Oracle合约账户
        private const string ORACLE_ACCOUNT = "oracle_account";

        //Tokenized合约账户
        private const string TOKENIZED_ACCOUNT = "tokenized_account";

        //admin账户
        private const string ADMIN_ACCOUNT = "admin_account";

        private const string SERVICE_FEE = "issuing_fee_b";

        private const string SAR_STATE = "sar_state";

        private const ulong FOURTEEN_POWER = 100000000000000;

        private static byte[] getSARKey(byte[] addr) => new byte[] { 0x12 }.Concat(addr);

        private static byte[] getTxidKey(byte[] txid) => new byte[] { 0x14 }.Concat(txid);

        private static byte[] getAccountKey(byte[] key) => new byte[] { 0x15 }.Concat(key);

        private static byte[] getConfigKey(byte[] key) => new byte[] { 0x17 }.Concat(key);

        private static byte[] getRedeemKey(byte[] addr) => new byte[] { 0x18 }.Concat(addr);


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
            TRANSACTION_TYPE_REDEEM,//用户 赎回
            TRANSACTION_TYPE_DESTORY//销毁

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
            var magicstr = "2018-09-12 14:45";

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
                    if (args.Length != 5) return false;
                    string name = (string)args[0];
                    string symbol = (string)args[1];
                    byte decimals = (byte)args[2];
                    byte[] addr = (byte[])args[3];
                    string anchor = (string)args[4];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return openSAR4B(name, symbol, decimals, addr, anchor);
                }
                //迁移SAR记录
                if (operation == "createSAR4B")
                {
                    if (args.Length != 9) return false;
                    byte[] addr = (byte[])args[0];
                    byte[] txid = (byte[])args[1];
                    string name = (string)args[2];
                    string symbol = (string)args[3];
                    byte decimals = (byte)args[4];
                    BigInteger locked = (BigInteger)args[5];
                    BigInteger hasDrawed = (BigInteger)args[6];
                    int status = (int)args[7];
                    string anchor = (string)args[8];

                    if (!Runtime.CheckWitness(addr)) return false;

                    SARInfo sar = new SARInfo();
                    sar.symbol = symbol;
                    sar.anchor = anchor;
                    sar.name = name;
                    sar.decimals = decimals;
                    sar.locked = locked;
                    sar.hasDrawed = hasDrawed;
                    sar.owner = addr;
                    sar.status = status;
                    sar.txid = txid;
                    return createSAR4B(addr, sar);
                }
                //转移SAR合约中NEP5资产
                if (operation == "migrateSAR4B")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return migrateSAR4B(addr);
                }
                if (operation == "initToken")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return initToken(addr);
                }
                //锁定资产
                if (operation == "reserve")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                   
                    if (!Runtime.CheckWitness(addr)) return false;
                    return reserve(name, addr, value);
                }
                //增加
                if (operation == "expande")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                   
                    if (!Runtime.CheckWitness(addr)) return false;
                    return expande(name, addr, value);
                }
                //擦除
                if (operation == "contract")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return contract(name, addr, value);
                }
                //提现
                if (operation == "withdraw")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return withdraw(name, addr, value);
                }
                //提现清算额
                if (operation == "claimRedeem")
                {
                    if (args.Length != 1) return false;
                    byte[] addr = (byte[])args[0];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return claimRedeem(addr);
                }
                if (operation == "setAccount")
                {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    byte[] address = (byte[])args[1];

                    if (!checkAdmin()) return false;
                    return setAccount(key,address);
                }
                //个人用户赎回不安全的仓位
                if (operation == "redeem")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    byte[] sarAddr = (byte[])args[1];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return redeem(addr, sarAddr);

                }
                //修改SAR状态为关闭
                if (operation == "settingSAR")
                {
                    if (args.Length != 2) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];

                    if (!checkAdmin()) return false;
                    return settingSAR(name, addr);
                }
                //设置全局参数
                if (operation == "setConfig")
                {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    BigInteger value = (BigInteger)args[1];

                    if (!checkAdmin()) return false;
                    return setConfig(key, value);
                }
                //查询全局参数
                if (operation == "getConfig")
                {
                    if (args.Length != 1) return false;
                    string key = (string)args[0];

                    return getConfig(key);
                }
                //销毁
                if (operation == "destory")
                {
                    if (args.Length != 2) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                   
                    if (!Runtime.CheckWitness(addr)) return false;
                    return destory(name, addr);
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
                    //1|2|4
                    bool need_storage = (bool)(object)07;
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

        private static bool createSAR4B(byte[] addr, SARInfo sar)
        {
            //SAR是否存在
            byte[] key = getSARKey(addr);

            byte[] sarCurr = Storage.Get(Storage.CurrentContext, key);
            if (sarCurr.Length > 0)
                return false;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sar));
            return true;
        }

        private static bool migrateSAR4B(byte[] addr)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            //check SAR 
            if (checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST be pause.");

            //check SAR
            var key = getSARKey(addr);
            byte[] bytes = Storage.Get(Storage.CurrentContext, key);
            if (bytes.Length == 0) throw new InvalidOperationException("The sar can not be null.");

            SARInfo sarInfo = Helper.Deserialize(bytes) as SARInfo;

            BigInteger locked = sarInfo.locked;
            BigInteger hasDrawed = sarInfo.hasDrawed;

            byte[] newSARID = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT_NEW.AsByteArray()));
            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));

            {
                object[] arg = new object[3];
                arg[0] = from;
                arg[1] = newSARID;
                arg[2] = locked;
                var nep5Contract = (NEP5Contract)sdsAssetID.ToDelegate();
                if (!(bool)nep5Contract("transfer_contract", arg)) throw new InvalidOperationException("The sar operation is exception.");
            }

            {
                var newContract = (NEP5Contract)newSARID.ToDelegate();
                object[] args = new object[9];
                args[0] = addr;
                args[1] = sarInfo.txid;
                args[2] = sarInfo.name;
                args[3] = sarInfo.symbol;
                args[4] = sarInfo.decimals;
                args[5] = sarInfo.locked;
                args[6] = sarInfo.hasDrawed;
                args[7] = sarInfo.status;
                args[8] = sarInfo.anchor;

                if (!(bool)newContract("createSAR4B", args)) throw new InvalidOperationException("The sar operation is exception.");
            }
            Storage.Delete(Storage.CurrentContext, key);
            return true;
        }

        public static bool setAccount(string key,byte[] address)
        {
            if (key==null || key =="") return false;

            if (address.Length != 20) return false;

            Storage.Put(Storage.CurrentContext, getAccountKey(key.AsByteArray()), address);
            return true;
        }

        /*查询债仓详情*/
        public static SARInfo getSAR4B(byte[] addr)
        {
            var key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) return null;

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            return info;
        }

        public static bool settingSAR(string name, byte[] addr)
        {
            if (addr.Length != 20) throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            var key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) throw new InvalidOperationException("The sar must not be null.");

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) throw new InvalidOperationException("The sar must be self.");

            info.status = (int)ConfigSARStatus.SAR_STATUS_CLOSE;

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));
            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_SHUT;
            detail.operated = 0;
            detail.hasLocked = info.locked;
            detail.hasDrawed = info.hasDrawed;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(info.name.AsByteArray(), admin, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_SHUT, 0);
            return true;
        }

        /*提现抵押资产*/
        public static bool withdraw(string name, byte[] addr, BigInteger value)
        {
            if (addr.Length != 20) throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (value <= 0) throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST be pause.");

            var key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) throw new InvalidOperationException("The sar must not be null.");

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) throw new InvalidOperationException("The sar must be self.");

            //SAR状态
            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE) throw new InvalidOperationException("The sar status must not be close.");

            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));
            byte[] to = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));

            BigInteger locked = info.locked;
            BigInteger hasDrawed = info.hasDrawed;

            object[] arg = new object[0];
            BigInteger sds_price = 0;
            BigInteger anchor_price = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                //调用Oracle,查询SDS价格，如：8$=价格*100000000
                arg = new object[1];
                arg[0] = CONFIG_SDS_PRICE;
                sds_price = (BigInteger)OracleContract("getTypeB", arg);
            }

            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                //调用Oracle,查询锚定价格，如：100$=价格*100000000
                arg = new object[1];
                arg[0] = info.anchor;
                anchor_price = (BigInteger)OracleContract("getTypeB", arg);
            }
            //当前兑换率，默认是100，需要从配置中心获取
            BigInteger rate = 100;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                //调用Oracle,查询抵押率，如：50 => 50%
                arg = new object[1];
                arg[0] = CONFIG_LIQUIDATION_RATE_B;
                BigInteger re = (BigInteger)OracleContract("getTypeA", arg);
                if (re != 0)
                    rate = re;
            }

            //当前兑换率，默认是100，需要从配置中心获取
            BigInteger serviceFree = 1000000000;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                //调用Oracle,查询手续费，如：1000000000
                arg = new object[1];
                arg[0] = SERVICE_FEE;
                BigInteger re = (BigInteger)OracleContract("getTypeB", arg);
                if (re != 0)
                    serviceFree = re;
            }

            //计算已经兑换SDS量
            BigInteger hasDrawSDS = hasDrawed * rate * FOURTEEN_POWER / (sds_price * anchor_price);

            //释放的总量大于已经剩余，不能操作
            if (value > locked - hasDrawSDS) throw new InvalidOperationException("The param is exception.");

            //剩余必须大于
            if (locked - value < serviceFree) throw new InvalidOperationException("The param is exception.");

            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            object[] param = new object[3];
            param[0] = from;
            param[1] = addr;
            param[2] = value;

            var SDSContract = (NEP5Contract)sdsAssetID.ToDelegate();

            if (!(bool)SDSContract("transfer_contract", param)) throw new InvalidOperationException("The operation is exception.");

            //重新设置锁定量
            info.locked = locked - value;

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
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_WITHDRAW, value);
            return true;
        }


        public static bool destory(string name, byte[] addr)
        {
            if (addr.Length != 20) throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST be pause.");

            var key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) throw new InvalidOperationException("The sar must not be null.");

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) throw new InvalidOperationException("The operation is exception.");

            //SAR状态
            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE) throw new InvalidOperationException("The operation is exception.");
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));
            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));

            BigInteger locked = info.locked;
            BigInteger hasDrawed = info.hasDrawed;

            object[] arg = new object[0];
            BigInteger sds_price = 0;
            BigInteger anchor_price = 0;


            //当前兑换率，默认是100，需要从配置中心获取
            BigInteger serviceFree = 1000000000;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                //调用Oracle,查询手续费，如：1000000000
                arg = new object[1];
                arg[0] = SERVICE_FEE;
                BigInteger re = (BigInteger)OracleContract("getTypeA", arg);
                if (re != 0)
                    serviceFree = re;
            }

            //总量大于固定费率，不能操作
            if (locked > serviceFree) throw new InvalidOperationException("The param is exception.");


            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            object[] param = new object[3];
            param[0] = from;
            param[1] = addr;
            param[2] = locked;

            var SDSContract = (NEP5Contract)sdsAssetID.ToDelegate();

            if (!(bool)SDSContract("transfer_contract", param)) throw new InvalidOperationException("The operation is exception.");

            param = new object[2];
            param[0] = name;
            param[1] = addr;

            var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
            if (!(bool)TokenizedContract("close", param)) throw new InvalidOperationException("The operation is exception.");

            info.locked = 0;
            info.status = (int)ConfigSARStatus.SAR_STATUS_CLOSE;

            //Storage.Put(Storage.CurrentContext,key, Helper.Serialize(info));
            Storage.Delete(Storage.CurrentContext,key);
            //记录交易详细数据
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_DESTORY;
            detail.operated = locked;
            detail.hasLocked = 0;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_DESTORY, locked);
            return true;
        }

        /*返还部分仓位*/
        public static bool contract(string name, byte[] addr, BigInteger value)
        {
            if (addr.Length != 20) throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (value <= 0) throw new InvalidOperationException("The param is exception.");

            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST be pause.");

            var key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) throw new InvalidOperationException("The sar must not be null.");

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger()) throw new InvalidOperationException("The param is exception.");

            //SAR状态
            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE) throw new InvalidOperationException("The param is exception.");

            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));

            byte[] to = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));

            BigInteger hasDrawed = info.hasDrawed;
            //不能超过已经获取
            if (value > hasDrawed) throw new InvalidOperationException("The param is exception.");

            object[] arg = new object[3];
            arg[0] = name;
            arg[1] = addr;
            arg[2] = value;

            var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();

            if (!(bool)TokenizedContract("destory", arg)) throw new InvalidOperationException("The param is exception.");

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
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_CONTRACT, value);
            return true;
        }

        /*开启一个新的债仓*/
        public static bool openSAR4B(string name, string symbol, byte decimals, byte[] addr, string anchor)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");
            //check SAR 
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST be pause.");

            //判断该地址是否拥有SAR
            var key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length > 0)
                throw new InvalidOperationException("The sar must be null.");

            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));

            {
                //调用标准合约
                object[] arg = new object[1];
                arg[0] = name;
                //验证name
                var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
                string str = (string)TokenizedContract("name", arg);
                if (str.Length > 0) throw new InvalidOperationException("The name must be null.");
            }

            {
                //判断是否是白名单
                object[] arg = new object[1];
                arg[0] = anchor;

                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                BigInteger re = (BigInteger)OracleContract("getTypeA", arg);
                if (re != 1) throw new InvalidOperationException("The param is exception.");
            }

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
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

            //交易详细信息
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_OPEN;
            detail.operated = 0;
            detail.hasLocked = 0;
            detail.hasDrawed = 0;

            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(name.AsByteArray(), addr, txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_OPEN, 0);
            return true;
        }

        public static bool initToken(byte[] addr)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            //check SAR 
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST be pause.");

            //判断该地址是否拥有SAR
            var key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                throw new InvalidOperationException("The sar must not be null.");

            SARInfo info = (SARInfo)Helper.Deserialize(sar);

            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));
            byte[] to = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));

            //验证name
            object[] arg = new object[1];
            arg[0] = info.name;

            var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
            string str = (string)TokenizedContract("name", arg);
            if (str.Length > 0) throw new InvalidOperationException("The name must be null.");

            //当前兑换率，默认是10，需要从配置中心获取
            BigInteger serviceFree = 1000000000;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                //调用Oracle,查询手续费
                arg = new object[1];
                arg[0] = SERVICE_FEE;
                BigInteger re = (BigInteger)OracleContract("getTypeA", arg);
                if (re != 0)
                    serviceFree = re;
            }

            //转入抵押手续费
            arg = new object[3];
            arg[0] = addr;
            arg[1] = to;
            arg[2] = serviceFree;

            var SDSContract = (NEP5Contract)sdsAssetID.ToDelegate();
            if (!(bool)SDSContract("transfer", arg)) throw new InvalidOperationException("The operation is exception.");

            info.locked = info.locked + serviceFree;

            arg = new object[4];
            arg[0] = info.name;
            arg[1] = info.symbol;
            arg[2] = info.decimals;
            arg[3] = addr;

            //保存标准
            var TokenizedContract2 = (NEP5Contract)tokenizedAssetID.ToDelegate();
            if (!(bool)TokenizedContract2("init", arg)) throw new InvalidOperationException("The operation is exception.");

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            //触发操作事件
            Operated(info.name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_INIT, 0);

            Nep55Operated(info.name.AsByteArray(),addr, info.txid,info.anchor.AsByteArray(),info.symbol.AsByteArray(),8);
            return true;
        }

        /*向债仓锁定数字资产*/
        public static bool reserve(string name, byte[] addr, BigInteger value)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (value <= 0)
                throw new InvalidOperationException("The parameter is exception.");

            //check SAR 
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST be pause.");

            var key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) throw new InvalidOperationException("The sar must not be null.");

            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger())
                throw new InvalidOperationException("The parameter is exception.");
            //SAR状态
            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE)
                throw new InvalidOperationException("The parameter is exception.");

            byte[] to = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            object[] arg = new object[3];
            arg[0] = addr;
            arg[1] = to;
            arg[2] = value;

            var SDSContract = (NEP5Contract)sdsAssetID.ToDelegate();

            if (!(bool)SDSContract("transfer", arg)) throw new InvalidOperationException("The operation is exception.");

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
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(info.name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_RESERVE, value);
            return true;
        }

        public static bool expande(string name, byte[] addr, BigInteger value)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (value <= 0)
                throw new InvalidOperationException("The parameter is exception.");

            //check SAR 
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST be pause.");

            var key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) throw new InvalidOperationException("The sar must not be null.");

            SARInfo info = (SARInfo)Helper.Deserialize(sar);
            // SAR制造者操作
            if (info.owner.AsBigInteger() != addr.AsBigInteger())
                throw new InvalidOperationException("The parameter is exception.");

            //SAR状态
            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE)
                throw new InvalidOperationException("The parameter is exception.");

            BigInteger locked = info.locked;
            BigInteger hasDrawed = info.hasDrawed;

            BigInteger sds_price = 0;
            BigInteger anchor_price = 0;

            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));

            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                //调用Oracle,查询SDS价格，如：8$=价格*100000000
                object[] arg = new object[1];
                arg[0] = CONFIG_SDS_PRICE;
                sds_price = (BigInteger)OracleContract("getTypeB", arg);
            }

            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                //调用Oracle,查询锚定价格，如：100$=价格*100000000
                object[]  arg = new object[1];
                arg[0] = info.anchor;
                anchor_price = (BigInteger)OracleContract("getTypeB", arg);
            }

            //当前兑换率，默认是100，需要从配置中心获取
            BigInteger sds_rate = 100;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                //调用Oracle,查询抵押率，如：50 => 50%
                object[] arg = new object[1];
                arg[0] = CONFIG_LIQUIDATION_RATE_B;
                BigInteger re = (BigInteger)OracleContract("getTypeA", arg);
                if (re != 0)
                    sds_rate = re;
            }

            BigInteger sdusd_limit = sds_price * locked * 100 / (anchor_price * sds_rate);

            if (sdusd_limit < hasDrawed + value)
                throw new InvalidOperationException("The parameter is exception.");

            {
                //调用标准增发
                object[] arg = new object[3];
                arg[0] = name;
                arg[1] = addr;
                arg[2] = value;

                var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
                if (!(bool)TokenizedContract("increase", arg)) throw new InvalidOperationException("The parameter is exception.");
            }

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

            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //触发操作事件
            Operated(info.name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_EXPANDE, value);
            return true;
        }

        public static bool redeem(byte[] addr, byte[] sarAddr)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (sarAddr.Length != 20)
                throw new InvalidOperationException("The parameter sarAddr SHOULD be 20-byte addresses.");

            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST be pause.");

            var key = getSARKey(sarAddr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0) throw new InvalidOperationException("The sar must not be null.");

            SARInfo info = (SARInfo)Helper.Deserialize(sar);

            //SAR状态,必须是关闭状态
            if (info.status != (int)ConfigSARStatus.SAR_STATUS_CLOSE)
                throw new InvalidOperationException("The parameter is exception.");

            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));

            //转账给用户SDS
            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));

            //清算的币种
            string name = info.name;

            //调用标准查询余额
            object[] arg = new object[2];
            arg[0] = name;
            arg[1] = addr;
            BigInteger balance = 0;
            {
                var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
                balance = (BigInteger)TokenizedContract("balanceOf", arg);
                if (balance <= 0) throw new InvalidOperationException("The parameter is exception.");
            }

            BigInteger totalSupply = 0;
            {
                arg = new object[1];
                arg[0] = name;
                var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
                totalSupply = (BigInteger)TokenizedContract("totalSupply", arg);
                if (totalSupply <= 0) throw new InvalidOperationException("The parameter is exception.");
            }

            //计算可赎回的SDS
            BigInteger redeem = balance * info.locked/ totalSupply;
            if (redeem <= 0) throw new InvalidOperationException("The parameter is exception.");

            //销毁用户的稳定代币
            arg = new object[3];
            arg[0] = name;
            arg[1] = addr;
            arg[2] = balance;

            var TokenizedContract2 = (NEP5Contract)tokenizedAssetID.ToDelegate();
            if (!(bool)TokenizedContract2("destory", arg)) throw new InvalidOperationException("The parameter is exception.");

            info.locked = info.locked - redeem;
            info.hasDrawed = info.hasDrawed - balance;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

            var redeemKey = getRedeemKey(addr);
            BigInteger redeemBalance = Storage.Get(Storage.CurrentContext, redeemKey).AsBigInteger();
            Storage.Put(Storage.CurrentContext,redeemKey,redeemBalance + redeem);

            //记录交易详细数据
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //触发操作事件
            Operated(info.name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_REDEEM, redeem);
            return true;
        }

        private static bool claimRedeem(byte[] addr) {
            var redeemKey = getRedeemKey(addr);
            BigInteger redeemBalance = Storage.Get(Storage.CurrentContext, redeemKey).AsBigInteger();

            byte[] from = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT.AsByteArray()));
            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));

            object[] param = new object[3];
            param[0] = from;
            param[1] = addr;
            param[2] = redeemBalance;

            var SDSContract = (NEP5Contract)sdsAssetID.ToDelegate();
            if (!(bool)SDSContract("transfer_contract", param)) throw new InvalidOperationException("The parameter is exception.");

            Storage.Delete(Storage.CurrentContext, redeemKey);
            return true;
        }

        private static BigInteger getConfig(string configKey)
        {
            byte[] key = getConfigKey(configKey.AsByteArray());
            StorageMap config = Storage.CurrentContext.CreateMap(nameof(config));

            return config.Get(key).AsBigInteger();
        }

        private static Boolean setConfig(string configKey, BigInteger value)
        {
            byte[] key = getConfigKey(configKey.AsByteArray());
            StorageMap config = Storage.CurrentContext.CreateMap(nameof(config));
            config.Put(key, value);
            return true;
        }

        //checkState 1:normal  0:stop
        private static bool checkState(string configKey)
        {
            byte[] key = getConfigKey(configKey.AsByteArray());
            StorageMap config = Storage.CurrentContext.CreateMap(nameof(config));
            BigInteger value = config.Get(key).AsBigInteger();

            if (value == 1) return true;
            return false;
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

            //已经提取的资产，如SDUSD
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

            //已经提取的资产金额，如SDUSD
            public BigInteger hasDrawed;

            //操作类型
            public int type;
        }
    }
}
