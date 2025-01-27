//
//      Copyright (C) DataStax Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

using System;
using System.Collections.Generic;
using System.IO;
using Cassandra.Serialization;

namespace Cassandra.Requests
{
    /// <summary>
    /// Represents a protocol EXECUTE request
    /// </summary>
    internal class ExecuteRequest : IQueryRequest, ICqlRequest
    {
        public const byte OpCode = 0x0A;
        private FrameHeader.HeaderFlag _headerFlags;
        private readonly byte[] _id;
        private readonly QueryProtocolOptions _queryOptions;

        public ConsistencyLevel Consistency 
        { 
            get { return _queryOptions.Consistency; }
            set { _queryOptions.Consistency = value; }
        }

        public byte[] PagingState
        {
            get { return _queryOptions.PagingState; }
            set { _queryOptions.PagingState = value; }
        }

        public int PageSize
        {
            get { return _queryOptions.PageSize; }
        }

        public ConsistencyLevel SerialConsistency
        {
            get { return _queryOptions.SerialConsistency; }
        }

        public IDictionary<string, byte[]> Payload { get; set; }

        public ExecuteRequest(ProtocolVersion protocolVersion, byte[] id, RowSetMetadata metadata, bool tracingEnabled, QueryProtocolOptions queryOptions)
        {
            if (metadata != null && queryOptions.Values.Length != metadata.Columns.Length)
            {
                throw new ArgumentException("Number of values does not match with number of prepared statement markers(?).");
            }
            _id = id;
            _queryOptions = queryOptions;
            if (tracingEnabled)
            {
                _headerFlags = FrameHeader.HeaderFlag.Tracing;
            }

            if (queryOptions.SerialConsistency != ConsistencyLevel.Any && queryOptions.SerialConsistency.IsSerialConsistencyLevel() == false)
            {
                throw new RequestInvalidException("Non-serial consistency specified as a serial one.");
            }
            if (queryOptions.RawTimestamp != null && !protocolVersion.SupportsTimestamp())
            {
                throw new NotSupportedException("Timestamp for query is supported in Cassandra 2.1 or above.");
            }
        }

        public int WriteFrame(short streamId, MemoryStream stream, ISerializer serializer)
        {
            var wb = new FrameWriter(stream, serializer);
            if (Payload != null)
            {
                _headerFlags |= FrameHeader.HeaderFlag.CustomPayload;
            }
            wb.WriteFrameHeader((byte)_headerFlags, streamId, OpCode);
            if (Payload != null)
            {
                //A custom payload for this request
                wb.WriteBytesMap(Payload);
            }
            wb.WriteShortBytes(_id);
            _queryOptions.Write(wb, true);
            return wb.Close();
        }

        public void WriteToBatch(FrameWriter wb)
        {
            wb.WriteByte(1); //prepared query
            wb.WriteShortBytes(_id);
            wb.WriteUInt16((ushort)_queryOptions.Values.Length);
            foreach (var queryParameter in _queryOptions.Values)
            {
                wb.WriteAsBytes(queryParameter);
            }
        }
    }
}
