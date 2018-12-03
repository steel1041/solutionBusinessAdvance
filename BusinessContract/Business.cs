using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;
using Helper = Neo.SmartContract.Framework.Helper;
using System.ComponentModel;
using Neo.SmartContract.Framework.Services.System;

namespace SAR4B
{
    public class SAR4B : SmartContract
    {
        //Default multiple signature committee account
        private static readonly byte[] committee = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        public delegate object NEP5Contract(string method, object[] args);

        /* name,addr,sartxid,txid,type,operated*/
        [DisplayName("sarOperator")]
        public static event Action<byte[], byte[], byte[], byte[], BigInteger, BigInteger> Operated;

        /* name,addr,sartxid,anchor,symbol,decimals*/
        [DisplayName("nep55Operator")]
        public static event Action<byte[], byte[], byte[], byte[], byte[], BigInteger> Nep55Operated;

        /** 
         * Static param
         */
        //price of SDS
        private const string CONFIG_SDS_PRICE = "sds_price";

        //risk management
        //private const string CONFIG_SDS_RATE = "config_sds_rate";
        private const string CONFIG_LIQUIDATION_RATE_B = "liquidate_line_rate_b";
        private const string SERVICE_FEE = "issuing_fee_b";
        private const string SAR_STATE = "sar_state";
        private const string NAME_PREFIX = "SD-";

        //for upgrade
        private const string STORAGE_ACCOUNT_NEW = "storage_account_new";
        private const string STORAGE_ACCOUNT_OLD = "storage_account_old";

        //system account
        private const string SDS_ACCOUNT = "sds_account";
        private const string ORACLE_ACCOUNT = "oracle_account";
        private const string TOKENIZED_ACCOUNT = "tokenized_account";
        private const string ADMIN_ACCOUNT = "admin_account";

        //system param
        private const ulong FOURTEEN_POWER = 100000000000000;

        /*     
        * Key wrapper
        */
        private static byte[] getSARKey(byte[] addr) => new byte[] { 0x12 }.Concat(addr);
        private static byte[] getTxidKey(byte[] txid) => new byte[] { 0x14 }.Concat(txid);
        private static byte[] getAccountKey(byte[] key) => new byte[] { 0x15 }.Concat(key);
        private static byte[] getConfigKey(byte[] key) => new byte[] { 0x17 }.Concat(key);
        private static byte[] getRedeemKey(byte[] addr) => new byte[] { 0x18 }.Concat(addr);

        //Transaction type
        public enum ConfigTranType
        {
            TRANSACTION_TYPE_OPEN = 1,
            TRANSACTION_TYPE_INIT,
            TRANSACTION_TYPE_RESERVE,
            TRANSACTION_TYPE_EXPANDE,
            TRANSACTION_TYPE_WITHDRAW,
            TRANSACTION_TYPE_CONTRACT,
            TRANSACTION_TYPE_SHUT,
            TRANSACTION_TYPE_REDEEM,
            TRANSACTION_TYPE_DESTORY
        }

        //SAR states
        public enum ConfigSARStatus
        {
            SAR_STATUS_SAFETY = 1,  //安全
            SAR_STATUS_RISK,        //危险
            SAR_STATUS_CLOSE,       //不可用
        }


        public static Object Main(string operation, params object[] args)
        {
            var magicstr = "2018-11-23 14:45";

            if (Runtime.Trigger == TriggerType.Verification)
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                var callscript = ExecutionEngine.CallingScriptHash;

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
                /**An example of upgrade 'accept' method, the createSARBC interface should been 
                *  implemented in the following new SARBC contract
                */
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

                    byte[] account = Storage.Get(Storage.CurrentContext, getAccountKey(STORAGE_ACCOUNT_OLD.AsByteArray()));
                    if (account.AsBigInteger() != callscript.AsBigInteger()) return false;

                    return createSAR4B(addr, sar);
                }
                //Migrate SAR account to new contract by owner of SAR
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

                //lock SDS to SAR as collateral
                if (operation == "reserve")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return reserve(name, addr, value);
                }
                //issue stablecoin
                if (operation == "expande")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return expande(name, addr, value);
                }
                //payback stablecoin
                if (operation == "contract")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return contract(name, addr, value);
                }
                //get SDS from SAR
                if (operation == "withdraw")
                {
                    if (args.Length != 3) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return withdraw(name, addr, value);
                }

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
                    //only committee account
                    if (!checkAdmin()) return false;
                    return setAccount(key, address);
                }
                //stablecoin holder can redeem locked SDS if this SAR is unsafe
                if (operation == "redeem")
                {
                    if (args.Length != 2) return false;
                    byte[] addr = (byte[])args[0];
                    byte[] sarAddr = (byte[])args[1];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return redeem(addr, sarAddr);

                }

                if (operation == "settingSAR")
                {
                    if (args.Length != 2) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];
                    //only committee account
                    if (!checkAdmin()) return false;
                    return settingSAR(name, addr);
                }

                if (operation == "setConfig")
                {
                    if (args.Length != 2) return false;
                    string key = (string)args[0];
                    BigInteger value = (BigInteger)args[1];
                    //only committee account
                    if (!checkAdmin()) return false;
                    return setConfig(key, value);
                }

                if (operation == "destory")
                {
                    if (args.Length != 2) return false;
                    string name = (string)args[0];
                    byte[] addr = (byte[])args[1];

                    if (!Runtime.CheckWitness(addr)) return false;
                    return destory(name, addr);
                }

                if (operation == "getConfig")
                {
                    if (args.Length != 1) return false;
                    string key = (string)args[0];

                    return getConfig(key);
                }

                if (operation == "getSARTxInfo")
                {
                    if (args.Length != 1) return false;
                    byte[] txid = (byte[])args[0];
                    return getSARTxInfo(txid);
                }
                #region contract upgrade
                if (operation == "upgrade")
                {
                    //only committee account
                    if (!checkAdmin()) return false;

                    if (args.Length != 1 && args.Length != 9)
                        return false;

                    byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
                    byte[] new_script = (byte[])args[0];

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

        private static SARTransferDetail getSARTxInfo(byte[] txid)
        {
            byte[] v = Storage.Get(Storage.CurrentContext, getTxidKey(txid));
            if (v.Length == 0)
                return null;
            return (SARTransferDetail)Helper.Deserialize(v);
        }

        private static bool checkAdmin()
        {
            byte[] currAdmin = Storage.Get(Storage.CurrentContext, getAccountKey(ADMIN_ACCOUNT.AsByteArray()));
            if (currAdmin.Length > 0)
            {
                if (!Runtime.CheckWitness(currAdmin)) return false;
            }
            else
            {
                if (!Runtime.CheckWitness(committee)) return false;
            }
            return true;
        }

        /**
         * check name of stablecoin
         */
        private static bool checkName(string name)
        {
            foreach (var c in name)
            {
                if ('A' <= c && c <= 'Z')
                {
                    continue;
                }
                else if ('a' <= c && c <= 'z')
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private static bool createSAR4B(byte[] addr, SARInfo sar)
        {
            byte[] key = getSARKey(addr);

            byte[] sarCurr = Storage.Get(Storage.CurrentContext, key);
            if (sarCurr.Length > 0)
                return false;
            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(sar));

            //notify
            Operated(sar.name.AsByteArray(), addr, txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_OPEN, 0);
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
            byte[] from = ExecutionEngine.ExecutingScriptHash;
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
            return true;
        }

        public static bool setAccount(string key, byte[] address)
        {
            if (key == null || key == "") return false;

            if (address.Length != 20) return false;

            Storage.Put(Storage.CurrentContext, getAccountKey(key.AsByteArray()), address);
            return true;
        }

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

            if (info.owner.AsBigInteger() != addr.AsBigInteger()) throw new InvalidOperationException("The sar must be self.");

            info.status = (int)ConfigSARStatus.SAR_STATUS_CLOSE;

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_SHUT;
            detail.operated = 0;
            detail.hasLocked = info.locked;
            detail.hasDrawed = info.hasDrawed;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
            Operated(info.name.AsByteArray(), committee, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_SHUT, 0);
            return true;
        }

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

            if (info.owner.AsBigInteger() != addr.AsBigInteger()) throw new InvalidOperationException("The sar must be self.");

            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE) throw new InvalidOperationException("The sar status must not be close.");

            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));

            BigInteger locked = info.locked;
            BigInteger hasDrawed = info.hasDrawed;

            object[] arg = new object[0];
            BigInteger sds_price = 0;
            BigInteger anchor_price = 0;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                arg = new object[1];
                arg[0] = CONFIG_SDS_PRICE;
                sds_price = (BigInteger)OracleContract("getTypeB", arg);
            }

            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                arg = new object[1];
                arg[0] = info.anchor;
                anchor_price = (BigInteger)OracleContract("getTypeB", arg);
            }

            BigInteger rate = 100;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                arg = new object[1];
                arg[0] = CONFIG_LIQUIDATION_RATE_B;
                BigInteger re = (BigInteger)OracleContract("getTypeA", arg);
                if (re != 0)
                    rate = re;
            }

            BigInteger serviceFree = 1000000000;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                arg = new object[1];
                arg[0] = SERVICE_FEE;
                BigInteger re = (BigInteger)OracleContract("getTypeA", arg);
                if (re != 0)
                    serviceFree = re;
            }

            BigInteger hasDrawSDS = hasDrawed * rate * FOURTEEN_POWER / (sds_price * anchor_price);

            if (value > locked - hasDrawSDS) throw new InvalidOperationException("The param is exception.");

            if (locked - value < serviceFree) throw new InvalidOperationException("The param is exception.");

            byte[] from = ExecutionEngine.ExecutingScriptHash;

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            object[] param = new object[3];
            param[0] = from;
            param[1] = addr;
            param[2] = value;

            var SDSContract = (NEP5Contract)sdsAssetID.ToDelegate();

            if (!(bool)SDSContract("transfer_contract", param)) throw new InvalidOperationException("The operation is exception.");

            info.locked = locked - value;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_WITHDRAW;
            detail.operated = value;
            detail.hasLocked = locked - value;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
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

            if (info.owner.AsBigInteger() != addr.AsBigInteger()) throw new InvalidOperationException("The operation is exception.");

            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE) throw new InvalidOperationException("The operation is exception.");
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));
            byte[] from = ExecutionEngine.ExecutingScriptHash;

            BigInteger locked = info.locked;
            BigInteger hasDrawed = info.hasDrawed;

            object[] arg = new object[0];

            BigInteger serviceFree = 1000000000;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                arg = new object[1];
                arg[0] = SERVICE_FEE;
                BigInteger re = (BigInteger)OracleContract("getTypeA", arg);
                if (re != 0)
                    serviceFree = re;
            }

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

            Storage.Delete(Storage.CurrentContext, key);

            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_DESTORY;
            detail.operated = locked;
            detail.hasLocked = 0;
            detail.hasDrawed = hasDrawed;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
            Operated(name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_DESTORY, locked);
            return true;
        }

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

            if (info.owner.AsBigInteger() != addr.AsBigInteger()) throw new InvalidOperationException("The param is exception.");

            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE) throw new InvalidOperationException("The param is exception.");

            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));

            BigInteger hasDrawed = info.hasDrawed;

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

            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_CONTRACT;
            detail.operated = value;
            detail.hasLocked = info.locked;
            detail.hasDrawed = info.hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
            Operated(name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_CONTRACT, value);
            return true;
        }

        public static bool openSAR4B(string name, string symbol, byte decimals, byte[] addr, string anchor)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");
            //check SAR 
            if (!checkState(SAR_STATE))
                throw new InvalidOperationException("The sar state MUST be pause.");

            if (name.Length <= 0)
                throw new InvalidOperationException("The parameter name MUST bigger than 0.");

            if (!checkName(name))
                throw new InvalidOperationException("The parameter name is invalid.");

            var key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length > 0)
                throw new InvalidOperationException("The sar must be null.");

            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));

            {

                object[] arg = new object[1];
                arg[0] = string.Concat(NAME_PREFIX, name);

                var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
                string str = (string)TokenizedContract("name", arg);
                if (str.Length > 0) throw new InvalidOperationException("The name must be null.");
            }

            {

                object[] arg = new object[1];
                arg[0] = anchor;

                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();
                BigInteger re = (BigInteger)OracleContract("getTypeA", arg);
                if (re != 1) throw new InvalidOperationException("The param is exception.");
            }

            byte[] txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARInfo info = new SARInfo();
            info.symbol = symbol;
            info.decimals = decimals;
            info.name = string.Concat(NAME_PREFIX, name);
            info.hasDrawed = 0;
            info.locked = 0;
            info.owner = addr;
            info.txid = txid;
            info.status = 1;
            info.anchor = anchor;

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_OPEN;
            detail.operated = 0;
            detail.hasLocked = 0;
            detail.hasDrawed = 0;

            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
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

            var key = getSARKey(addr);
            byte[] sar = Storage.Get(Storage.CurrentContext, key);
            if (sar.Length == 0)
                throw new InvalidOperationException("The sar must not be null.");

            SARInfo info = (SARInfo)Helper.Deserialize(sar);

            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));
            byte[] to = ExecutionEngine.ExecutingScriptHash;

            object[] arg = new object[1];
            arg[0] = info.name;

            var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
            string str = (string)TokenizedContract("name", arg);
            if (str.Length > 0)
                throw new InvalidOperationException("The name must be null.");

            BigInteger serviceFree = 1000000000;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                arg = new object[1];
                arg[0] = SERVICE_FEE;
                BigInteger re = (BigInteger)OracleContract("getTypeA", arg);
                if (re != 0)
                    serviceFree = re;
            }

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

            var TokenizedContract2 = (NEP5Contract)tokenizedAssetID.ToDelegate();
            if (!(bool)TokenizedContract2("init", arg)) throw new InvalidOperationException("The operation is exception.");

            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            Operated(info.name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_INIT, 0);

            Nep55Operated(info.name.AsByteArray(), addr, info.txid, info.anchor.AsByteArray(), info.symbol.AsByteArray(), info.decimals);
            return true;
        }

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

            if (info.owner.AsBigInteger() != addr.AsBigInteger())
                throw new InvalidOperationException("The parameter is exception.");

            if (info.status == (int)ConfigSARStatus.SAR_STATUS_CLOSE)
                throw new InvalidOperationException("The parameter is exception.");

            byte[] to = ExecutionEngine.ExecutingScriptHash;
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

            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_RESERVE;
            detail.operated = value;
            detail.hasLocked = currLock;
            detail.hasDrawed = info.hasDrawed;
            detail.txid = txid;
            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
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

            if (info.owner.AsBigInteger() != addr.AsBigInteger())
                throw new InvalidOperationException("The parameter is exception.");

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

                object[] arg = new object[1];
                arg[0] = CONFIG_SDS_PRICE;
                sds_price = (BigInteger)OracleContract("getTypeB", arg);
            }

            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

                object[] arg = new object[1];
                arg[0] = info.anchor;
                anchor_price = (BigInteger)OracleContract("getTypeB", arg);
            }

            BigInteger sds_rate = 100;
            {
                var OracleContract = (NEP5Contract)oracleAssetID.ToDelegate();

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

                object[] arg = new object[3];
                arg[0] = name;
                arg[1] = addr;
                arg[2] = value;

                var TokenizedContract = (NEP5Contract)tokenizedAssetID.ToDelegate();
                if (!(bool)TokenizedContract("increase", arg)) throw new InvalidOperationException("The parameter is exception.");
            }

            info.hasDrawed = hasDrawed + value;
            Storage.Put(Storage.CurrentContext, key, Helper.Serialize(info));


            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;
            SARTransferDetail detail = new SARTransferDetail();
            detail.from = addr;
            detail.sarTxid = info.txid;
            detail.txid = txid;
            detail.type = (int)ConfigTranType.TRANSACTION_TYPE_EXPANDE;
            detail.operated = value;
            detail.hasLocked = locked;
            detail.hasDrawed = info.hasDrawed;

            Storage.Put(Storage.CurrentContext, getTxidKey(txid), Helper.Serialize(detail));

            //notify
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

            if (info.status != (int)ConfigSARStatus.SAR_STATUS_CLOSE)
                throw new InvalidOperationException("The parameter is exception.");

            byte[] sdsAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(SDS_ACCOUNT.AsByteArray()));
            byte[] oracleAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(ORACLE_ACCOUNT.AsByteArray()));
            byte[] tokenizedAssetID = Storage.Get(Storage.CurrentContext, getAccountKey(TOKENIZED_ACCOUNT.AsByteArray()));

            byte[] from = ExecutionEngine.ExecutingScriptHash;

            string name = info.name;

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

            BigInteger redeem = balance * info.locked / totalSupply;
            if (redeem <= 0) throw new InvalidOperationException("The parameter is exception.");

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
            Storage.Put(Storage.CurrentContext, redeemKey, redeemBalance + redeem);

            var txid = ((Transaction)ExecutionEngine.ScriptContainer).Hash;

            //notify
            Operated(info.name.AsByteArray(), addr, info.txid, txid, (int)ConfigTranType.TRANSACTION_TYPE_REDEEM, redeem);
            return true;
        }

        private static bool claimRedeem(byte[] addr)
        {
            var redeemKey = getRedeemKey(addr);
            BigInteger redeemBalance = Storage.Get(Storage.CurrentContext, redeemKey).AsBigInteger();

            byte[] from = ExecutionEngine.ExecutingScriptHash;
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
            //stablecoin param
            public string name;
            public string symbol;
            public byte decimals;

            //creator
            public byte[] owner;

            //key of this SAR
            public byte[] txid;

            //amount of locked SDS
            public BigInteger locked;

            //amount of issued stablecoin
            public BigInteger hasDrawed;

            //1safe  2unsafe 3freeze   
            public int status;

            //anchored asset type 
            public string anchor;
        }

        public class SARTransferDetail
        {
            public byte[] from;

            public byte[] sarTxid;

            public byte[] txid;

            public BigInteger operated;

            public BigInteger hasLocked;

            public BigInteger hasDrawed;

            public int type;
        }
    }
}
