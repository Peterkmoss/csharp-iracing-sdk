using System;
using System.Linq;
using System.Collections.Generic;
using SharedMemory;

namespace csharp_iracing_sdk
{
    public class IRacingSDK {
        public const string VERSION = "0.0.1";
        public const string SIM_STATUS_URL = "http://127.0.0.1:32034/get_sim_status?object=simStatus";
        public const string BROADCAST_MESSAGE_NAME = "CSHARP_BROADCAST";

        public const string MEMORY_FILE = "Local\\IRacingSDKMemory";
        public const int MEMORY_SIZE = 1164 * 1024;

        private bool _isInitialized = false;
        private BufferReadWrite _sharedMemory;
        private Header _header;

        private VariableBuffer _variableBufferLatest;

        private VariableBuffer getLatestVariableBuffer() {
            if (!_isInitialized && !StartUp())
                return null;
            if (_variableBufferLatest != null)
                return _variableBufferLatest;
            return (from b in _header.VarBuffers
                    orderby b.TickCount(_header)
                    select b).First();
        }

        public bool StartUp() {
            _sharedMemory = new BufferReadWrite(MEMORY_FILE, MEMORY_SIZE);
            if (_sharedMemory == null)
                return false;

            _header = new Header(_sharedMemory);
            _isInitialized = _header.Version(_header) >= 1 && _header.VarBuffers.Count > 0;
            return _isInitialized;
        }

        public void ShutDown() {
            _isInitialized = false;
            _sharedMemory.Close();
            _sharedMemory = null;
            _header = null;
        }

        public bool IsConnected() {
            return _header != null && _header.Status(_header) == StatusField.CONNECTION_STATUS;
        }

        public void FreezeVariableBufferLatest() {
            UnfreezeVariableBufferLatest();
            _variableBufferLatest = getLatestVariableBuffer();
            _variableBufferLatest.Freeze();
        }

        public void UnfreezeVariableBufferLatest() {
            if (_variableBufferLatest != null) {
                _variableBufferLatest.UnFreeze();
                _variableBufferLatest = null;
            }
        }
    }

    class Header : SDKStruct {
        public Func<Header, dynamic> Version = SDKStruct.ReadValueClosure(0),
                                            Status = SDKStruct.ReadValueClosure(4),
                                            TickRate = SDKStruct.ReadValueClosure(8),
                                            SessionInfoUpdate = SDKStruct.ReadValueClosure(12),
                                            SessionInfoLength = SDKStruct.ReadValueClosure(16),
                                            SessionInfoOffset = SDKStruct.ReadValueClosure(20),
                                            NumberOfVars = SDKStruct.ReadValueClosure(24),
                                            VariableHeaderOffset = SDKStruct.ReadValueClosure(28),
                                            NumberOfBuffers = SDKStruct.ReadValueClosure(32),
                                            BufferLength = SDKStruct.ReadValueClosure(36);

        public ICollection<VariableBuffer> VarBuffers;
        public Header(BufferReadWrite sharedMemory, int offset = 0) : base(sharedMemory, offset) {
            for (var i = 0; i < NumberOfBuffers(this); i++) {
                VarBuffers.Add(new VariableBuffer(sharedMemory, 48 + i * 16));
            }
        }
    }

    class VariableBuffer : SDKStruct
    {
        public Func<Header, dynamic> TickCount = SDKStruct.ReadValueClosure(0), 
                                     BufferOffset = SDKStruct.ReadValueClosure(4);

        public VariableBuffer(BufferReadWrite sharedMemory, int offset = 0) : base(sharedMemory, offset) { }

        public void Freeze() {
            freezedMemory = sharedMemory;
        }

        public void UnFreeze() {
            freezedMemory = null;
        }

        public BufferReadWrite getMemory() {
            return freezedMemory != null ? freezedMemory : sharedMemory;
        }
    }

    class SDKStruct {
        protected int offset;
        protected BufferReadWrite sharedMemory;
        protected BufferReadWrite freezedMemory;

        public SDKStruct(BufferReadWrite sharedMemory, int offset = 0) {
            this.sharedMemory = sharedMemory;
            this.offset = offset;
        }

        public static Func<Header, dynamic> ReadValueClosure(int offset) {
            return (Header header) => {
                dynamic returnValue = null;
                header.sharedMemory.Read(returnValue, offset + offset);
                return returnValue;
            };
        }
    }

    class StatusField {
        public const int CONNECTION_STATUS = 1;
    }
}
