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

	using Document = Lucene.Net.Document.Document;
	using Field = Lucene.Net.Document.Field;
	using FieldType = Lucene.Net.Document.FieldType;
	using TextField = Lucene.Net.Document.TextField;
	using IndexSearcher = Lucene.Net.Search.IndexSearcher;
	using TermQuery = Lucene.Net.Search.TermQuery;
	using TopDocs = Lucene.Net.Search.TopDocs;
	using Directory = Lucene.Net.Store.Directory;
	using BytesRef = Lucene.Net.Util.BytesRef;
	using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
	using SuppressCodecs = Lucene.Net.Util.LuceneTestCase.SuppressCodecs;
    using NUnit.Framework;

	/// <summary>
	/// Test indexing and searching some byte[] terms
	/// </summary>
//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressCodecs("Lucene3x") public class TestBinaryTerms extends Lucene.Net.Util.LuceneTestCase
	public class TestBinaryTerms : LuceneTestCase
	{
	  public virtual void TestBinary()
	  {
		Directory dir = NewDirectory();
		RandomIndexWriter iw = new RandomIndexWriter(Random(), dir);
		BytesRef bytes = new BytesRef(2);
		BinaryTokenStream tokenStream = new BinaryTokenStream(bytes);

		for (int i = 0; i < 256; i++)
		{
		  bytes.Bytes[0] = (sbyte) i;
		  bytes.Bytes[1] = unchecked((sbyte)(255 - i));
		  bytes.Length = 2;
		  Document doc = new Document();
		  FieldType customType = new FieldType();
		  customType.Stored = true;
		  doc.Add(new Field("id", "" + i, customType));
		  doc.Add(new TextField("bytes", tokenStream));
		  iw.AddDocument(doc);
		}

		IndexReader ir = iw.Reader;
        iw.Close();

		IndexSearcher @is = NewSearcher(ir);

		for (int i = 0; i < 256; i++)
		{
		  bytes.Bytes[0] = (sbyte) i;
		  bytes.Bytes[1] = unchecked((sbyte)(255 - i));
		  bytes.Length = 2;
		  TopDocs docs = @is.Search(new TermQuery(new Term("bytes", bytes)), 5);
		  Assert.AreEqual(1, docs.TotalHits);
		  Assert.AreEqual("" + i, @is.Doc(docs.ScoreDocs[0].Doc).Get("id"));
		}

		ir.Dispose();
		dir.Dispose();
	  }

	  public virtual void TestToString()
	  {
		Term term = new Term("foo", new BytesRef(new sbyte[] {unchecked((sbyte) 0xff), unchecked((sbyte) 0xfe)}));
		Assert.AreEqual("foo:[ff fe]", term.ToString());
	  }
	}

}