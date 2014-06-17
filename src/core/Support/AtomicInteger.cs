﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Lucene.Net.Support
{
    public class AtomicInteger
    {
        private int value;
        public AtomicInteger()
            : this(0)
        {
        }

        public AtomicInteger(int value_) {
            Interlocked.Exchange(ref value, value_);
        }

        public int IncrementAndGet() 
        {
            return Interlocked.Increment(ref value);
        }

        public int DecrementAndGet()
        {
            return Interlocked.Decrement(ref value);
        }

        public void Set(int value_)
        {
            Interlocked.Exchange(ref value, value_);
        }

        public int AddAndGet(int value_)
        {
            Interlocked.Add(ref value, value_);
            return value;
        }

        public int Get()
        {
            //LUCENE TO-DO read operations atomic in 64 bit
            return value;
        }

        public bool CompareAndSet(int expect, int update)
        {
            int rc = Interlocked.CompareExchange(ref value, update, expect);
            return rc == expect;
        }
    }
}