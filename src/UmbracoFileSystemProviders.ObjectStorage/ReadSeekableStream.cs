namespace Our.Umbraco.FileSystemProviders.ObjectStorage
{
using System;
using System.IO;

	/// <summary>
    /// Creates a Stream wrapper.
    /// It is useful when it is important that the stream will not disposed after using.
    /// <para>
    /// Source: http://stackoverflow.com/a/28036366/1387407
    /// </para>
    /// </summary>
    public class ReadSeekableStream : Stream
    {
        private long _underlyingPosition;
        private readonly byte[] _seekBackBuffer;
        private int _seekBackBufferCount;
        private int _seekBackBufferIndex;
        private readonly Stream _underlyingStream;

        public ReadSeekableStream(Stream underlyingStream, int seekBackBufferSize)
        {
            if (!underlyingStream.CanRead)
            {
                throw new Exception("Provided stream " + underlyingStream + " is not readable");
            }

            this._underlyingStream = underlyingStream;
            this._seekBackBuffer = new byte[seekBackBufferSize];
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int copiedFromBackBufferCount = 0;
            if (this._seekBackBufferIndex < this._seekBackBufferCount)
            {
                copiedFromBackBufferCount = Math.Min(count, this._seekBackBufferCount - this._seekBackBufferIndex);
                Buffer.BlockCopy(this._seekBackBuffer, this._seekBackBufferIndex, buffer, offset, copiedFromBackBufferCount);
                offset += copiedFromBackBufferCount;
                count -= copiedFromBackBufferCount;
                this._seekBackBufferIndex += copiedFromBackBufferCount;
            }
            int bytesReadFromUnderlying = 0;
            if (count > 0)
            {
                bytesReadFromUnderlying = this._underlyingStream.Read(buffer, offset, count);
                if (bytesReadFromUnderlying > 0)
                {
                    this._underlyingPosition += bytesReadFromUnderlying;

                    var copyToBufferCount = Math.Min(bytesReadFromUnderlying, this._seekBackBuffer.Length);
                    var copyToBufferOffset = Math.Min(this._seekBackBufferCount, this._seekBackBuffer.Length - copyToBufferCount);
                    var bufferBytesToMove = Math.Min(this._seekBackBufferCount - 1, copyToBufferOffset);

                    if (bufferBytesToMove > 0)
                    {
                        Buffer.BlockCopy(this._seekBackBuffer, this._seekBackBufferCount - bufferBytesToMove, this._seekBackBuffer, 0, bufferBytesToMove);
                    }

                    Buffer.BlockCopy(buffer, offset, this._seekBackBuffer, copyToBufferOffset, copyToBufferCount);
                    this._seekBackBufferCount = Math.Min(this._seekBackBuffer.Length, this._seekBackBufferCount + copyToBufferCount);
                    this._seekBackBufferIndex = this._seekBackBufferCount;
                }
            }
            return copiedFromBackBufferCount + bytesReadFromUnderlying;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.End)
            {
                return this.SeekFromEnd((int) Math.Max(0, -offset));
            }

            var relativeOffset = origin == SeekOrigin.Current
                ? offset
                : offset - this.Position;

            if (relativeOffset == 0)
            {
                return this.Position;
            }
            else if (relativeOffset > 0)
            {
                return this.SeekForward(relativeOffset);
            }
			else
            {
                return this.SeekBackwards(-relativeOffset);
            }
        }

        private long SeekForward(long origOffset)
        {
            long offset = origOffset;
            var seekBackBufferLength = this._seekBackBuffer.Length;

            int backwardSoughtBytes = this._seekBackBufferCount - this._seekBackBufferIndex;
            int seekForwardInBackBuffer = (int) Math.Min(offset, backwardSoughtBytes);
            offset -= seekForwardInBackBuffer;
            this._seekBackBufferIndex += seekForwardInBackBuffer;

            if (offset > 0)
            {
                // first completely fill seekBackBuffer to remove special cases from while loop below
                if (this._seekBackBufferCount < seekBackBufferLength)
                {
                    var maxRead = seekBackBufferLength - this._seekBackBufferCount;
                    if (offset < maxRead)
                    {
                        maxRead = (int) offset;
                    }

                    var bytesRead = this._underlyingStream.Read(this._seekBackBuffer, this._seekBackBufferCount, maxRead);
                    this._underlyingPosition += bytesRead;
                    this._seekBackBufferCount += bytesRead;
                    this._seekBackBufferIndex = this._seekBackBufferCount;
                    if (bytesRead < maxRead)
                    {
                        if (this._seekBackBufferCount < offset)
                        {
                            throw new NotSupportedException("Reached end of stream seeking forward " + origOffset + " bytes");
                        }

                        return this.Position;
                    }
                    offset -= bytesRead;
                }

                // now alternate between filling tempBuffer and seekBackBuffer
                bool fillTempBuffer = true;
                var tempBuffer = new byte[seekBackBufferLength];
                while (offset > 0)
                {
                    var maxRead = offset < seekBackBufferLength ? (int) offset : seekBackBufferLength;
                    var bytesRead = this._underlyingStream.Read(fillTempBuffer ? tempBuffer : this._seekBackBuffer, 0, maxRead);
                    this._underlyingPosition += bytesRead;
                    var bytesReadDiff = maxRead - bytesRead;
                    offset -= bytesRead;
                    if (bytesReadDiff > 0 /* reached end-of-stream */ || offset == 0) 
                    {
                        if (fillTempBuffer)
                        {
                            if (bytesRead > 0)
                            {
                                Buffer.BlockCopy(this._seekBackBuffer, bytesRead, this._seekBackBuffer, 0, bytesReadDiff);
                                Buffer.BlockCopy(tempBuffer, 0, this._seekBackBuffer, bytesReadDiff, bytesRead);
                            }
                        }
                        else
                        {
                            if (bytesRead > 0)
                            {
                                Buffer.BlockCopy(this._seekBackBuffer, 0, this._seekBackBuffer, bytesReadDiff, bytesRead);
                            }

                            Buffer.BlockCopy(tempBuffer, bytesRead, this._seekBackBuffer, 0, bytesReadDiff);
                        }
                        if (offset > 0)
                        {
                            throw new NotSupportedException("Reached end of stream seeking forward " + origOffset + " bytes");
                        }
                    }
                    fillTempBuffer = !fillTempBuffer;
                }
            }
            return this.Position;
        }

        private long SeekBackwards(long offset)
        {
            var intOffset = (int)offset;
            if (offset > int.MaxValue || intOffset > this._seekBackBufferIndex)
            {
                throw new NotSupportedException("Cannot currently seek backwards more than " + this._seekBackBufferIndex + " bytes");
            }

            this._seekBackBufferIndex -= intOffset;
            return this.Position;
        }

        private long SeekFromEnd(long offset)
        {
            var intOffset = (int) offset;
            var seekBackBufferLength = this._seekBackBuffer.Length;
            if (offset > int.MaxValue || intOffset > seekBackBufferLength)
            {
                throw new NotSupportedException("Cannot seek backwards from end more than " + seekBackBufferLength + " bytes");
            }

            // first completely fill seekBackBuffer to remove special cases from while loop below
            if (this._seekBackBufferCount < seekBackBufferLength)
            {
                var maxRead = seekBackBufferLength - this._seekBackBufferCount;
                var bytesRead = this._underlyingStream.Read(this._seekBackBuffer, this._seekBackBufferCount, maxRead);
                this._underlyingPosition += bytesRead;
                this._seekBackBufferCount += bytesRead;
                this._seekBackBufferIndex = Math.Max(0, this._seekBackBufferCount - intOffset);
                if (bytesRead < maxRead)
                {
                    if (this._seekBackBufferCount < intOffset)
                    {
                        throw new NotSupportedException("Could not seek backwards from end " + intOffset + " bytes");
                    }

                    return this.Position;
                }
            }
            else
            {
                this._seekBackBufferIndex = this._seekBackBufferCount;
            }

            // now alternate between filling tempBuffer and seekBackBuffer
            bool fillTempBuffer = true;
            var tempBuffer = new byte[seekBackBufferLength];
            while (true)
            {
                var bytesRead = this._underlyingStream.Read(fillTempBuffer ? tempBuffer : this._seekBackBuffer, 0, seekBackBufferLength);
                this._underlyingPosition += bytesRead;
                var bytesReadDiff = seekBackBufferLength - bytesRead;
                if (bytesReadDiff > 0) // reached end-of-stream
                {
                    if (fillTempBuffer)
                    {
                        if (bytesRead > 0)
                        {
                            Buffer.BlockCopy(this._seekBackBuffer, bytesRead, this._seekBackBuffer, 0, bytesReadDiff);
                            Buffer.BlockCopy(tempBuffer, 0, this._seekBackBuffer, bytesReadDiff, bytesRead);
                        }
                    }
                    else
                    {
                        if (bytesRead > 0)
                        {
                            Buffer.BlockCopy(this._seekBackBuffer, 0, this._seekBackBuffer, bytesReadDiff, bytesRead);
                        }

                        Buffer.BlockCopy(tempBuffer, bytesRead, this._seekBackBuffer, 0, bytesReadDiff);
                    }
                    this._seekBackBufferIndex -= intOffset;
                    return this.Position;
                }
                fillTempBuffer = !fillTempBuffer;
            }
        }

        public override long Position
        {
            get { return this._underlyingPosition - (this._seekBackBufferCount - this._seekBackBufferIndex); }
            set { this.Seek(value, SeekOrigin.Begin); }
        }

        public override bool CanTimeout { get { return this._underlyingStream.CanTimeout; } }
        public override bool CanWrite { get { return this._underlyingStream.CanWrite; } }
        public override long Length { get { return this._underlyingStream.Length; } }
        public override void SetLength(long value) { this._underlyingStream.SetLength(value); }
        public override void Write(byte[] buffer, int offset, int count) { this._underlyingStream.Write(buffer, offset, count); }
        public override void Flush() { this._underlyingStream.Flush(); }
    }

    
}
