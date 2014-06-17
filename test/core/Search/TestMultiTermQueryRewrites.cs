using System;

namespace Lucene.Net.Search
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
	using DirectoryReader = Lucene.Net.Index.DirectoryReader;
	using IndexReader = Lucene.Net.Index.IndexReader;
	using MultiReader = Lucene.Net.Index.MultiReader;
	using Term = Lucene.Net.Index.Term;
	using Terms = Lucene.Net.Index.Terms;
	using TermsEnum = Lucene.Net.Index.TermsEnum;
	using RandomIndexWriter = Lucene.Net.Index.RandomIndexWriter;
	using Directory = Lucene.Net.Store.Directory;
	using AttributeSource = Lucene.Net.Util.AttributeSource;
	using BytesRef = Lucene.Net.Util.BytesRef;
	using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using NUnit.Framework;

	public class TestMultiTermQueryRewrites : LuceneTestCase
	{

	  internal static Directory Dir, Sdir1, Sdir2;
	  internal static IndexReader Reader, MultiReader, MultiReaderDupls;
	  internal static IndexSearcher Searcher, MultiSearcher, MultiSearcherDupls;

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @BeforeClass public static void beforeClass() throws Exception
	  public static void BeforeClass()
	  {
		Dir = NewDirectory();
		Sdir1 = NewDirectory();
		Sdir2 = NewDirectory();
		RandomIndexWriter writer = new RandomIndexWriter(Random(), Dir, new MockAnalyzer(Random()));
		RandomIndexWriter swriter1 = new RandomIndexWriter(Random(), Sdir1, new MockAnalyzer(Random()));
		RandomIndexWriter swriter2 = new RandomIndexWriter(Random(), Sdir2, new MockAnalyzer(Random()));

		for (int i = 0; i < 10; i++)
		{
		  Document doc = new Document();
		  doc.Add(NewStringField("data", Convert.ToString(i), Field.Store.NO));
		  writer.AddDocument(doc);
		  ((i % 2 == 0) ? swriter1 : swriter2).AddDocument(doc);
		}
		writer.ForceMerge(1);
		swriter1.ForceMerge(1);
		swriter2.ForceMerge(1);
		writer.Close();
        swriter1.Close();
        swriter2.Close();

		Reader = DirectoryReader.Open(Dir);
		Searcher = NewSearcher(Reader);

		MultiReader = new MultiReader(new IndexReader[] {DirectoryReader.Open(Sdir1), DirectoryReader.Open(Sdir2)}, true);
		MultiSearcher = NewSearcher(MultiReader);

		MultiReaderDupls = new MultiReader(new IndexReader[] {DirectoryReader.Open(Sdir1), DirectoryReader.Open(Dir)}, true);
		MultiSearcherDupls = NewSearcher(MultiReaderDupls);
	  }

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @AfterClass public static void afterClass() throws Exception
	  public static void AfterClass()
	  {
		Reader.Dispose();
		MultiReader.Dispose();
		MultiReaderDupls.Dispose();
		Dir.Dispose();
		Sdir1.Dispose();
		Sdir2.Dispose();
		Reader = MultiReader = MultiReaderDupls = null;
		Searcher = MultiSearcher = MultiSearcherDupls = null;
		Dir = Sdir1 = Sdir2 = null;
	  }

	  private Query ExtractInnerQuery(Query q)
	  {
		if (q is ConstantScoreQuery)
		{
		  // wrapped as ConstantScoreQuery
		  q = ((ConstantScoreQuery) q).Query;
		}
		return q;
	  }

	  private Term ExtractTerm(Query q)
	  {
		q = ExtractInnerQuery(q);
		return ((TermQuery) q).Term;
	  }

	  private void CheckBooleanQueryOrder(Query q)
	  {
		q = ExtractInnerQuery(q);
		BooleanQuery bq = (BooleanQuery) q;
		Term last = null, act ;
		foreach (BooleanClause clause in bq.Clauses)
		{
		  act = ExtractTerm(clause.Query);
		  if (last != null)
		  {
			Assert.IsTrue(last.CompareTo(act) < 0, "sort order of terms in BQ violated");
		  }
		  last = act;
		}
	  }

	  private void CheckDuplicateTerms(MultiTermQuery.RewriteMethod method)
	  {
		MultiTermQuery mtq = TermRangeQuery.NewStringRange("data", "2", "7", true, true);
		mtq.SetRewriteMethod(method);
		Query q1 = Searcher.Rewrite(mtq);
		Query q2 = MultiSearcher.Rewrite(mtq);
		Query q3 = MultiSearcherDupls.Rewrite(mtq);
		if (VERBOSE)
		{
		  Console.WriteLine();
		  Console.WriteLine("single segment: " + q1);
		  Console.WriteLine("multi segment: " + q2);
		  Console.WriteLine("multi segment with duplicates: " + q3);
		}
		Assert.AreEqual(q1, q2, "The multi-segment case must produce same rewritten query");
		Assert.AreEqual(q1, q3, "The multi-segment case with duplicates must produce same rewritten query");
		CheckBooleanQueryOrder(q1);
		CheckBooleanQueryOrder(q2);
		CheckBooleanQueryOrder(q3);
	  }

	  public virtual void TestRewritesWithDuplicateTerms()
	  {
		CheckDuplicateTerms(MultiTermQuery.SCORING_BOOLEAN_QUERY_REWRITE);

		CheckDuplicateTerms(MultiTermQuery.CONSTANT_SCORE_BOOLEAN_QUERY_REWRITE);

		// use a large PQ here to only test duplicate terms and dont mix up when all scores are equal
		CheckDuplicateTerms(new MultiTermQuery.TopTermsScoringBooleanQueryRewrite(1024));
		CheckDuplicateTerms(new MultiTermQuery.TopTermsBoostOnlyBooleanQueryRewrite(1024));

		// Test auto rewrite (but only boolean mode), so we set the limits to large values to always get a BQ
		MultiTermQuery.ConstantScoreAutoRewrite rewrite = new MultiTermQuery.ConstantScoreAutoRewrite();
		rewrite.TermCountCutoff = int.MaxValue;
		rewrite.DocCountPercent = 100.0;
		CheckDuplicateTerms(rewrite);
	  }

	  private void CheckBooleanQueryBoosts(BooleanQuery bq)
	  {
		foreach (BooleanClause clause in bq.Clauses)
		{
		  TermQuery mtq = (TermQuery) clause.Query;
		  Assert.AreEqual(Convert.ToSingle(mtq.Term.Text()), mtq.Boost, 0, "Parallel sorting of boosts in rewrite mode broken");
		}
	  }

	  private void CheckBoosts(MultiTermQuery.RewriteMethod method)
	  {
		MultiTermQuery mtq = new MultiTermQueryAnonymousInnerClassHelper(this);
		mtq.SetRewriteMethod(method);
		Query q1 = Searcher.Rewrite(mtq);
		Query q2 = MultiSearcher.Rewrite(mtq);
		Query q3 = MultiSearcherDupls.Rewrite(mtq);
		if (VERBOSE)
		{
		  Console.WriteLine();
		  Console.WriteLine("single segment: " + q1);
		  Console.WriteLine("multi segment: " + q2);
		  Console.WriteLine("multi segment with duplicates: " + q3);
		}
		Assert.AreEqual(q1, q2, "The multi-segment case must produce same rewritten query");
		Assert.AreEqual(q1, q3, "The multi-segment case with duplicates must produce same rewritten query");
		CheckBooleanQueryBoosts((BooleanQuery) q1);
		CheckBooleanQueryBoosts((BooleanQuery) q2);
		CheckBooleanQueryBoosts((BooleanQuery) q3);
	  }

	  private class MultiTermQueryAnonymousInnerClassHelper : MultiTermQuery
	  {
		  private readonly TestMultiTermQueryRewrites OuterInstance;

		  public MultiTermQueryAnonymousInnerClassHelper(TestMultiTermQueryRewrites outerInstance) : base("data")
		  {
			  this.OuterInstance = outerInstance;
		  }

		  protected internal override TermsEnum GetTermsEnum(Terms terms, AttributeSource atts)
		  {
			return new TermRangeTermsEnumAnonymousInnerClassHelper(this, terms.Iterator(null), new BytesRef("2"), new BytesRef("7"));
		  }

		  private class TermRangeTermsEnumAnonymousInnerClassHelper : TermRangeTermsEnum
		  {
			  private readonly MultiTermQueryAnonymousInnerClassHelper OuterInstance;

			  public TermRangeTermsEnumAnonymousInnerClassHelper(MultiTermQueryAnonymousInnerClassHelper outerInstance, TermsEnum iterator, BytesRef bref1, BytesRef bref2) 
                  : base(iterator, bref1, bref2, true, true)
			  {
				  this.OuterInstance = outerInstance;
				  boostAtt = Attributes().AddAttribute<BoostAttribute>();
			  }

			  internal readonly BoostAttribute boostAtt;

			  protected internal override AcceptStatus Accept(BytesRef term)
			  {
				boostAtt.Boost = Convert.ToSingle(term.Utf8ToString());
				return base.Accept(term);
			  }
		  }

		  public override string ToString(string field)
		  {
			return "dummy";
		  }
	  }

	  public virtual void TestBoosts()
	  {
		CheckBoosts(MultiTermQuery.SCORING_BOOLEAN_QUERY_REWRITE);

		// use a large PQ here to only test boosts and dont mix up when all scores are equal
		CheckBoosts(new MultiTermQuery.TopTermsScoringBooleanQueryRewrite(1024));
	  }

	  private void CheckMaxClauseLimitation(MultiTermQuery.RewriteMethod method)
	  {
		int savedMaxClauseCount = BooleanQuery.MaxClauseCount;
		BooleanQuery.MaxClauseCount = 3;

		MultiTermQuery mtq = TermRangeQuery.NewStringRange("data", "2", "7", true, true);
		mtq.SetRewriteMethod(method);
		try
		{
		  MultiSearcherDupls.Rewrite(mtq);
		  Assert.Fail("Should throw BooleanQuery.TooManyClauses");
		}
		catch (BooleanQuery.TooManyClauses e)
		{
		  //  Maybe remove this assert in later versions, when internal API changes:
		  Assert.AreEqual("Should throw BooleanQuery.TooManyClauses with a stacktrace containing checkMaxClauseCount()", "checkMaxClauseCount", e.StackTrace[0].MethodName);
		}
		finally
		{
		  BooleanQuery.MaxClauseCount = savedMaxClauseCount;
		}
	  }

	  private void CheckNoMaxClauseLimitation(MultiTermQuery.RewriteMethod method)
	  {
		int savedMaxClauseCount = BooleanQuery.MaxClauseCount;
		BooleanQuery.MaxClauseCount = 3;

		MultiTermQuery mtq = TermRangeQuery.NewStringRange("data", "2", "7", true, true);
		mtq.SetRewriteMethod(method);
		try
		{
		  MultiSearcherDupls.Rewrite(mtq);
		}
		finally
		{
		  BooleanQuery.MaxClauseCount = savedMaxClauseCount;
		}
	  }

	  public virtual void TestMaxClauseLimitations()
	  {
		CheckMaxClauseLimitation(MultiTermQuery.SCORING_BOOLEAN_QUERY_REWRITE);
		CheckMaxClauseLimitation(MultiTermQuery.CONSTANT_SCORE_BOOLEAN_QUERY_REWRITE);

		CheckNoMaxClauseLimitation(MultiTermQuery.CONSTANT_SCORE_FILTER_REWRITE);
		CheckNoMaxClauseLimitation(MultiTermQuery.CONSTANT_SCORE_AUTO_REWRITE_DEFAULT);
		CheckNoMaxClauseLimitation(new MultiTermQuery.TopTermsScoringBooleanQueryRewrite(1024));
		CheckNoMaxClauseLimitation(new MultiTermQuery.TopTermsBoostOnlyBooleanQueryRewrite(1024));
	  }

	}

}