using System;

namespace Lucene.Net.Index
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

	using MockAnalyzer = Lucene.Net.Analysis.MockAnalyzer;
	using Document = Lucene.Net.Document.Document;
	using Field = Lucene.Net.Document.Field;
	using TextField = Lucene.Net.Document.TextField;
	using CollectionStatistics = Lucene.Net.Search.CollectionStatistics;
	using TermStatistics = Lucene.Net.Search.TermStatistics;
	using DefaultSimilarity = Lucene.Net.Search.Similarities.DefaultSimilarity;
	using PerFieldSimilarityWrapper = Lucene.Net.Search.Similarities.PerFieldSimilarityWrapper;
	using Similarity = Lucene.Net.Search.Similarities.Similarity;
	using Directory = Lucene.Net.Store.Directory;
	using LineFileDocs = Lucene.Net.Util.LineFileDocs;
	using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
	using SuppressCodecs = Lucene.Net.Util.LuceneTestCase.SuppressCodecs;
	using TestUtil = Lucene.Net.Util.TestUtil;
    using NUnit.Framework;
    using Lucene.Net.Support;

	/// 
//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressCodecs("Lucene3x") public class TestCustomNorms extends Lucene.Net.Util.LuceneTestCase
	public class TestCustomNorms : LuceneTestCase
	{
	  internal readonly string FloatTestField = "normsTestFloat";
	  internal readonly string ExceptionTestField = "normsTestExcp";

	  public virtual void TestFloatNorms()
	  {

		Directory dir = NewDirectory();
		MockAnalyzer analyzer = new MockAnalyzer(Random());
		analyzer.MaxTokenLength = TestUtil.NextInt(Random(), 1, IndexWriter.MAX_TERM_LENGTH);

		IndexWriterConfig config = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
		Similarity provider = new MySimProvider(this);
		config.SetSimilarity(provider);
		RandomIndexWriter writer = new RandomIndexWriter(Random(), dir, config);
		LineFileDocs docs = new LineFileDocs(Random());
		int num = AtLeast(100);
		for (int i = 0; i < num; i++)
		{
		  Document doc = docs.NextDoc();
		  float nextFloat = Random().nextFloat();
		  Field f = new TextField(FloatTestField, "" + nextFloat, Field.Store.YES);
		  f.Boost = nextFloat;

		  doc.Add(f);
		  writer.AddDocument(doc);
		  doc.RemoveField(FloatTestField);
		  if (Rarely())
		  {
			writer.Commit();
		  }
		}
		writer.Commit();
        writer.Close();
		AtomicReader open = SlowCompositeReaderWrapper.Wrap(DirectoryReader.Open(dir));
		NumericDocValues norms = open.GetNormValues(FloatTestField);
		Assert.IsNotNull(norms);
		for (int i = 0; i < open.MaxDoc(); i++)
		{
		  Document document = open.Document(i);
		  float expected = Convert.ToSingle(document.Get(FloatTestField));
		  Assert.AreEqual(expected, Number.IntBitsToFloat((int)norms.Get(i)), 0.0f);
		}
		open.Dispose();
		dir.Dispose();
        docs.Close();
	  }

	  public class MySimProvider : PerFieldSimilarityWrapper
	  {
		  private readonly TestCustomNorms OuterInstance;

		  public MySimProvider(TestCustomNorms outerInstance)
		  {
			  this.OuterInstance = outerInstance;
		  }

		internal Similarity @delegate = new DefaultSimilarity();

		public override float QueryNorm(float sumOfSquaredWeights)
		{
		  return @delegate.QueryNorm(sumOfSquaredWeights);
		}

		public override Similarity Get(string field)
		{
		  if (OuterInstance.FloatTestField.Equals(field))
		  {
			return new FloatEncodingBoostSimilarity();
		  }
		  else
		  {
			return @delegate;
		  }
		}

		public override float Coord(int overlap, int maxOverlap)
		{
		  return @delegate.Coord(overlap, maxOverlap);
		}
	  }

	  public class FloatEncodingBoostSimilarity : Similarity
	  {

		public override long ComputeNorm(FieldInvertState state)
		{
		  return Number.FloatToIntBits(state.Boost);
		}

		public override SimWeight ComputeWeight(float queryBoost, CollectionStatistics collectionStats, params TermStatistics[] termStats)
		{
		  throw new System.NotSupportedException();
		}

		public override SimScorer SimScorer(SimWeight weight, AtomicReaderContext context)
		{
		  throw new System.NotSupportedException();
		}
	  }
	}

}