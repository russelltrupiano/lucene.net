using System;
using System.Diagnostics;

namespace Lucene.Net.Analysis
{

    using System.IO;
    /*
         * Licensed to the Apache Software Foundation (ASF) under one or more
         * contributor license agreements.  See the NOTICE file distributed with
         * this work for additional information regarding copyright ownership.
         * The ASF licenses this file to You under the Apache License, Version 2.0
         * (the "License"); you may not use this file except in compliance with
         * the License.  You may obtain a copy of the License at
         *
         *     http://www.apache.org/licenses/LICENSE-2.0
         *
         * Unless required by applicable law or agreed to in writing, software
         * distributed under the License is distributed on an "AS IS" BASIS,
         * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
         * See the License for the specific language governing permissions and
         * limitations under the License.
         */


    using TestUtil = Lucene.Net.Util.TestUtil;

	/// <summary>
	/// Wraps a Reader, and can throw random or fixed
	///  exceptions, and spoon feed read chars. 
	/// </summary>

	public class MockReaderWrapper : StreamReader
	{

      private readonly StreamReader @in;
	  private readonly Random Random;

	  private int ExcAtChar = -1;
	  private int ReadSoFar;
	  private bool ThrowExcNext_Renamed;

      public MockReaderWrapper(Random random, StreamReader @in)
	  {
		this.@in = @in;
		this.Random = random;
	  }

	  /// <summary>
	  /// Throw an exception after reading this many chars. </summary>
	  public virtual void ThrowExcAfterChar(int charUpto)
	  {
		ExcAtChar = charUpto;
		// You should only call this on init!:
		Debug.Assert(ReadSoFar == 0);
	  }

	  public virtual void ThrowExcNext()
	  {
		ThrowExcNext_Renamed = true;
	  }

	  public override void Close()
	  {
		@in.Close();
	  }

	  public override int Read(char[] cbuf, int off, int len)
	  {
		if (ThrowExcNext_Renamed || (ExcAtChar != -1 && ReadSoFar >= ExcAtChar))
		{
		  throw new Exception("fake exception now!");
		}
		int read;
		int realLen;
		if (len == 1)
		{
		  realLen = 1;
		}
		else
		{
		  // Spoon-feed: intentionally maybe return less than
		  // the consumer asked for
		  realLen = TestUtil.NextInt(Random, 1, len);
		}
		if (ExcAtChar != -1)
		{
		  int left = ExcAtChar - ReadSoFar;
		  Debug.Assert(left != 0);
		  read = @in.Read(cbuf, off, Math.Min(realLen, left));
		  Debug.Assert(read != -1);
		  ReadSoFar += read;
		}
		else
		{
		  read = @in.Read(cbuf, off, realLen);
		}
		return read;
	  }

	  public override bool MarkSupported()
	  {
		return false;
	  }

	  public override bool Ready()
	  {
		return false;
	  }

	  public static bool IsMyEvilException(Exception t)
	  {
		return (t is Exception) && "fake exception now!".Equals(t.Message);
	  }
	}

}