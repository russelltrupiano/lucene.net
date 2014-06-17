namespace Lucene.Net.Analysis
{
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


	using CharTermAttribute = Lucene.Net.Analysis.Tokenattributes.CharTermAttribute;
	using PayloadAttribute = Lucene.Net.Analysis.Tokenattributes.PayloadAttribute;
	using PositionIncrementAttribute = Lucene.Net.Analysis.Tokenattributes.PositionIncrementAttribute;
	using BytesRef = Lucene.Net.Util.BytesRef;
    using Lucene.Net.Util;
    using System.IO;



	/// <summary>
	/// Wraps a whitespace tokenizer with a filter that sets
	/// the first token, and odd tokens to posinc=1, and all others
	/// to 0, encoding the position as pos: XXX in the payload.
	/// 
	/// </summary>
	public sealed class MockPayloadAnalyzer : Analyzer
	{

	  protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
	  {
		Tokenizer result = new MockTokenizer(reader, MockTokenizer.WHITESPACE, true);
		return new TokenStreamComponents(result, new MockPayloadFilter(result, fieldName));
	  }
	}

	/// 
	/// 
	/// 
	internal sealed class MockPayloadFilter : TokenFilter
	{
	  internal string FieldName;

	  internal int Pos;

	  internal int i;

	  internal readonly PositionIncrementAttribute PosIncrAttr;
	  internal readonly PayloadAttribute PayloadAttr;
	  internal readonly CharTermAttribute TermAttr;

	  public MockPayloadFilter(TokenStream input, string fieldName) : base(input)
	  {
		this.FieldName = fieldName;
		Pos = 0;
		i = 0;
		PosIncrAttr = input.AddAttribute<PositionIncrementAttribute>();
		PayloadAttr = input.AddAttribute<PayloadAttribute>();
		TermAttr = input.AddAttribute<CharTermAttribute>();
	  }

	  public override bool IncrementToken()
	  {
		if (Input.IncrementToken())
		{
		  PayloadAttr.Payload = new BytesRef(("pos: " + Pos)/*.getBytes(IOUtils.CHARSET_UTF_8)*/);
		  int posIncr;
		  if (Pos == 0 || i % 2 == 1)
		  {
			posIncr = 1;
		  }
		  else
		  {
			posIncr = 0;
		  }
		  PosIncrAttr.PositionIncrement = posIncr;
		  Pos += posIncr;
		  i++;
		  return true;
		}
		else
		{
		  return false;
		}
	  }

	  public override void Reset()
	  {
		base.Reset();
		i = 0;
		Pos = 0;
	  }
	}


}