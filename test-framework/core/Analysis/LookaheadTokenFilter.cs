using System;
using System.Diagnostics;
using System.Collections.Generic;

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


	using OffsetAttribute = Lucene.Net.Analysis.Tokenattributes.OffsetAttribute;
	using PositionIncrementAttribute = Lucene.Net.Analysis.Tokenattributes.PositionIncrementAttribute;
	using PositionLengthAttribute = Lucene.Net.Analysis.Tokenattributes.PositionLengthAttribute;
	using AttributeSource = Lucene.Net.Util.AttributeSource;
	using RollingBuffer = Lucene.Net.Util.RollingBuffer;

	// TODO: cut SynFilter over to this
	// TODO: somehow add "nuke this input token" capability...

	/// <summary>
	/// An abstract TokenFilter to make it easier to build graph
	///  token filters requiring some lookahead.  this class handles
	///  the details of buffering up tokens, recording them by
	///  position, restoring them, providing access to them, etc. 
	/// </summary>

	public abstract class LookaheadTokenFilter<T> : TokenFilter where T : LookaheadTokenFilter.Position
	{

	  private const bool DEBUG = false;

	  protected internal readonly PositionIncrementAttribute PosIncAtt = AddAttribute<PositionIncrementAttribute>();
	  protected internal readonly PositionLengthAttribute PosLenAtt = addAttribute(typeof(PositionLengthAttribute));
	  protected internal readonly OffsetAttribute OffsetAtt = addAttribute(typeof(OffsetAttribute));

	  // Position of last read input token:
	  protected internal int InputPos;

	  // Position of next possible output token to return:
	  protected internal int OutputPos;

	  // True if we hit end from our input:
	  protected internal bool End;

	  private bool TokenPending;
	  private bool InsertPending;

	  /// <summary>
	  /// Holds all state for a single position; subclass this
	  ///  to record other state at each position. 
	  /// </summary>
	  protected internal class Position : RollingBuffer.Resettable
	  {
		// Buffered input tokens at this position:
		public readonly IList<AttributeSource.State> InputTokens = new List<AttributeSource.State>();

		// Next buffered token to be returned to consumer:
		public int NextRead;

		// Any token leaving from this position should have this startOffset:
		public int StartOffset = -1;

		// Any token arriving to this position should have this endOffset:
		public int EndOffset = -1;

		public override void Reset()
		{
		  InputTokens.Clear();
		  NextRead = 0;
		  StartOffset = -1;
		  EndOffset = -1;
		}

		public virtual void Add(AttributeSource.State state)
		{
		  InputTokens.Add(state);
		}

		public virtual AttributeSource.State NextState()
		{
		  Debug.Assert(NextRead < InputTokens.Count);
		  return InputTokens[NextRead++];
		}
	  }

	  protected internal LookaheadTokenFilter(TokenStream input) : base(input)
	  {
	  }

	  /// <summary>
	  /// Call this only from within afterPosition, to insert a new
	  ///  token.  After calling this you should set any
	  ///  necessary token you need. 
	  /// </summary>
	  protected internal virtual void InsertToken()
	  {
		if (TokenPending)
		{
		  positions.get(InputPos).add(CaptureState());
		  TokenPending = false;
		}
		Debug.Assert(!InsertPending);
		InsertPending = true;
	  }

	  /// <summary>
	  /// this is called when all input tokens leaving a given
	  ///  position have been returned.  Override this and
	  ///  call insertToken and then set whichever token's
	  ///  attributes you want, if you want to inject
	  ///  a token starting from this position. 
	  /// </summary>
	  protected internal virtual void AfterPosition()
	  {
	  }

	  protected internal abstract T NewPosition();

	  protected internal readonly RollingBuffer<T> positions = new RollingBufferAnonymousInnerClassHelper();

	  private class RollingBufferAnonymousInnerClassHelper : RollingBuffer<T>
	  {
		  public RollingBufferAnonymousInnerClassHelper()
		  {
		  }

		  protected internal override T NewInstance()
		  {
			return OuterInstance.NewPosition();
		  }
	  }

	  /// <summary>
	  /// Returns true if there is a new token. </summary>
	  protected internal virtual bool PeekToken()
	  {
		if (DEBUG)
		{
		  Console.WriteLine("LTF.peekToken inputPos=" + InputPos + " outputPos=" + OutputPos + " tokenPending=" + TokenPending);
		}
		Debug.Assert(!End);
		Debug.Assert(InputPos == -1 || OutputPos <= InputPos);
		if (TokenPending)
		{
		  positions.get(InputPos).add(CaptureState());
		  TokenPending = false;
		}
		bool gotToken = Input.IncrementToken();
		if (DEBUG)
		{
		  Console.WriteLine("  input.incrToken() returned " + gotToken);
		}
		if (gotToken)
		{
		  InputPos += PosIncAtt.PositionIncrement;
		  Debug.Assert(InputPos >= 0);
		  if (DEBUG)
		  {
			Console.WriteLine("  now inputPos=" + InputPos);
		  }

		  Position startPosData = positions.get(InputPos);
		  Position endPosData = positions.get(InputPos + PosLenAtt.PositionLength);

		  int startOffset = OffsetAtt.StartOffset();
		  if (startPosData.StartOffset == -1)
		  {
			startPosData.StartOffset = startOffset;
		  }
		  else
		  {
			// Make sure our input isn't messing up offsets:
			Debug.Assert(startPosData.StartOffset == startOffset, "prev startOffset=" + startPosData.StartOffset + " vs new startOffset=" + startOffset + " inputPos=" + InputPos);
		  }

		  int endOffset = OffsetAtt.EndOffset();
		  if (endPosData.EndOffset == -1)
		  {
			endPosData.EndOffset = endOffset;
		  }
		  else
		  {
			// Make sure our input isn't messing up offsets:
			Debug.Assert(endPosData.EndOffset == endOffset, "prev endOffset=" + endPosData.EndOffset + " vs new endOffset=" + endOffset + " inputPos=" + InputPos);
		  }

		  TokenPending = true;
		}
		else
		{
		  End = true;
		}

		return gotToken;
	  }

	  /// <summary>
	  /// Call this when you are done looking ahead; it will set
	  ///  the next token to return.  Return the boolean back to
	  ///  the caller. 
	  /// </summary>
	  protected internal virtual bool NextToken()
	  {
		//System.out.println("  nextToken: tokenPending=" + tokenPending);
		if (DEBUG)
		{
		  Console.WriteLine("LTF.nextToken inputPos=" + InputPos + " outputPos=" + OutputPos + " tokenPending=" + TokenPending);
		}

		Position posData = positions.get(OutputPos);

		// While loop here in case we have to
		// skip over a hole from the input:
		while (true)
		{

		  //System.out.println("    check buffer @ outputPos=" +
		  //outputPos + " inputPos=" + inputPos + " nextRead=" +
		  //posData.nextRead + " vs size=" +
		  //posData.inputTokens.size());

		  // See if we have a previously buffered token to
		  // return at the current position:
		  if (posData.NextRead < posData.InputTokens.Count)
		  {
			if (DEBUG)
			{
			  Console.WriteLine("  return previously buffered token");
			}
			// this position has buffered tokens to serve up:
			if (TokenPending)
			{
			  positions.get(InputPos).add(CaptureState());
			  TokenPending = false;
			}
			RestoreState(positions.get(OutputPos).nextState());
			//System.out.println("      return!");
			return true;
		  }

		  if (InputPos == -1 || OutputPos == InputPos)
		  {
			// No more buffered tokens:
			// We may still get input tokens at this position
			//System.out.println("    break buffer");
			if (TokenPending)
			{
			  // Fast path: just return token we had just incr'd,
			  // without having captured/restored its state:
			  if (DEBUG)
			  {
				Console.WriteLine("  pass-through: return pending token");
			  }
			  TokenPending = false;
			  return true;
			}
			else if (End || !PeekToken())
			{
			  if (DEBUG)
			  {
				Console.WriteLine("  END");
			  }
			  AfterPosition();
			  if (InsertPending)
			  {
				// Subclass inserted a token at this same
				// position:
				if (DEBUG)
				{
				  Console.WriteLine("  return inserted token");
				}
				Debug.Assert(InsertedTokenConsistent());
				InsertPending = false;
				return true;
			  }

			  return false;
			}
		  }
		  else
		  {
			if (posData.StartOffset != -1)
			{
			  // this position had at least one token leaving
			  if (DEBUG)
			  {
				Console.WriteLine("  call afterPosition");
			  }
			  AfterPosition();
			  if (InsertPending)
			  {
				// Subclass inserted a token at this same
				// position:
				if (DEBUG)
				{
				  Console.WriteLine("  return inserted token");
				}
				Debug.Assert(InsertedTokenConsistent());
				InsertPending = false;
				return true;
			  }
			}

			// Done with this position; move on:
			OutputPos++;
			if (DEBUG)
			{
			  Console.WriteLine("  next position: outputPos=" + OutputPos);
			}
			positions.freeBefore(OutputPos);
			posData = positions.get(OutputPos);
		  }
		}
	  }

	  // If subclass inserted a token, make sure it had in fact
	  // looked ahead enough:
	  private bool InsertedTokenConsistent()
	  {
		int posLen = PosLenAtt.PositionLength;
		Position endPosData = positions.get(OutputPos + posLen);
		Debug.Assert(endPosData.EndOffset != -1);
		Debug.Assert(OffsetAtt.EndOffset() == endPosData.EndOffset, "offsetAtt.endOffset=" + OffsetAtt.EndOffset() + " vs expected=" + endPosData.EndOffset);
		return true;
	  }

	  // TODO: end()?
	  // TODO: close()?

	  public override void Reset()
	  {
		base.Reset();
		positions.reset();
		InputPos = -1;
		OutputPos = 0;
		TokenPending = false;
		End = false;
	  }
	}

}