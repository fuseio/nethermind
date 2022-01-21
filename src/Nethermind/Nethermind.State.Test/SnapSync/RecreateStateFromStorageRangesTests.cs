//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.

#nullable disable 

using System;
using System.Collections.Generic;
using System.Linq;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.State;
using Nethermind.State.Proofs;
using Nethermind.State.SnapSync;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;
using NUnit.Framework;

namespace Nethermind.Store.Test
{
    [TestFixture]
    public class RecreateStateFromStorageRangesTests
    {

        private TrieStore _store;
        private StateTree _inputStateTree;
        private StorageTree _inputStorageTree;

        [OneTimeSetUp]
        public void Setup()
        {
            _store = new TrieStore(new MemDb(), LimboLogs.Instance);
            (_inputStateTree, _inputStorageTree) = TestItem.Tree.GetTrees(_store);
        }

        [Test]
        public void RecreateStorageStateFromOneRangeWithNonExistenceProof()
        {
            Keccak rootHash = _inputStorageTree!.RootHash;   // "..."

            AccountProofCollector accountProofCollector = new(TestItem.Tree.AccountAddress0.Bytes, new Keccak[] { Keccak.Zero, TestItem.Tree.SlotsWithPaths[5].KeyHash });
            _inputStateTree!.Accept(accountProofCollector, _inputStateTree.RootHash);
            var proof = accountProofCollector.BuildResult();

            MemDb db = new MemDb();
            TrieStore store = new TrieStore(db, LimboLogs.Instance);

            Keccak result = SnapProvider.AddStorageRange(store, 1, rootHash, Keccak.Zero, TestItem.Tree.SlotsWithPaths, proof!.StorageProofs![0].Proof!.Concat(proof!.StorageProofs![1].Proof!).ToArray());

            Assert.AreEqual(rootHash, result);
        }

        [Test]
        public void RecreateAccountStateFromOneRangeWithExistenceProof()
        {
            Keccak rootHash = _inputStorageTree!.RootHash;   // "..."

            AccountProofCollector accountProofCollector = new(TestItem.Tree.AccountAddress0.Bytes, new Keccak[] { TestItem.Tree.SlotsWithPaths[0].KeyHash, TestItem.Tree.SlotsWithPaths[5].KeyHash });
            _inputStateTree!.Accept(accountProofCollector, _inputStateTree.RootHash);
            var proof = accountProofCollector.BuildResult();

            MemDb db = new MemDb();
            TrieStore store = new TrieStore(db, LimboLogs.Instance);

            Keccak result = SnapProvider.AddStorageRange(store, 1, rootHash, Keccak.Zero, TestItem.Tree.SlotsWithPaths, proof!.StorageProofs![0].Proof!.Concat(proof!.StorageProofs![1].Proof!).ToArray());

            Assert.AreEqual(rootHash, result);
        }

        [Test]
        public void RecreateStorageStateFromOneRangeWithoutProof()
        {
            Keccak rootHash = _inputStorageTree!.RootHash;   // "..."

            MemDb db = new MemDb();
            TrieStore store = new TrieStore(db, LimboLogs.Instance);

            Keccak result = SnapProvider.AddStorageRange(store, 1, rootHash, TestItem.Tree.SlotsWithPaths[0].KeyHash, TestItem.Tree.SlotsWithPaths);

            Assert.AreEqual(rootHash, result);
        }

        [Test]
        public void RecreateAccountStateFromMultipleRange()
        {
            Keccak rootHash = _inputStorageTree!.RootHash;   // "..."

            // output state
            MemDb db = new MemDb();
            TrieStore store = new TrieStore(db, LimboLogs.Instance);

            AccountProofCollector accountProofCollector = new(TestItem.Tree.AccountAddress0.Bytes, new Keccak[] { Keccak.Zero, TestItem.Tree.SlotsWithPaths[1].KeyHash });
            _inputStateTree!.Accept(accountProofCollector, _inputStateTree.RootHash);
            var proof = accountProofCollector.BuildResult();

            Keccak result1 = SnapProvider.AddStorageRange(store, 1, rootHash, Keccak.Zero, TestItem.Tree.SlotsWithPaths[0..2], proof!.StorageProofs![0].Proof!.Concat(proof!.StorageProofs![1].Proof!).ToArray());

            accountProofCollector = new(TestItem.Tree.AccountAddress0.Bytes, new Keccak[] { TestItem.Tree.SlotsWithPaths[2].KeyHash, TestItem.Tree.SlotsWithPaths[3].KeyHash });
            _inputStateTree!.Accept(accountProofCollector, _inputStateTree.RootHash);
            proof = accountProofCollector.BuildResult();

            Keccak result2 = SnapProvider.AddStorageRange(store, 1, rootHash, TestItem.Tree.SlotsWithPaths[2].KeyHash, TestItem.Tree.SlotsWithPaths[2..4], proof!.StorageProofs![0].Proof!.Concat(proof!.StorageProofs![1].Proof!).ToArray());

            accountProofCollector = new(TestItem.Tree.AccountAddress0.Bytes, new Keccak[] { TestItem.Tree.SlotsWithPaths[4].KeyHash, TestItem.Tree.SlotsWithPaths[5].KeyHash });
            _inputStateTree!.Accept(accountProofCollector, _inputStateTree.RootHash);
            proof = accountProofCollector.BuildResult();

            Keccak result3 = SnapProvider.AddStorageRange(store, 1, rootHash, TestItem.Tree.SlotsWithPaths[4].KeyHash, TestItem.Tree.SlotsWithPaths[4..6], proof!.StorageProofs![0].Proof!.Concat(proof!.StorageProofs![1].Proof!).ToArray());

            Assert.AreEqual(rootHash, result1);
            Assert.AreEqual(rootHash, result2);
            Assert.AreEqual(rootHash, result3);
        }

        [Test]
        public void MissingAccountFromRange()
        {
            Keccak rootHash = _inputStorageTree!.RootHash;   // "..."

            // output state
            MemDb db = new MemDb();
            TrieStore store = new TrieStore(db, LimboLogs.Instance);

            AccountProofCollector accountProofCollector = new(TestItem.Tree.AccountAddress0.Bytes, new Keccak[] { Keccak.Zero, TestItem.Tree.SlotsWithPaths[1].KeyHash });
            _inputStateTree!.Accept(accountProofCollector, _inputStateTree.RootHash);
            var proof = accountProofCollector.BuildResult();

            Keccak result1 = SnapProvider.AddStorageRange(store, 1, rootHash, Keccak.Zero, TestItem.Tree.SlotsWithPaths[0..2], proof!.StorageProofs![0].Proof!.Concat(proof!.StorageProofs![1].Proof!).ToArray());

            accountProofCollector = new(TestItem.Tree.AccountAddress0.Bytes, new Keccak[] { TestItem.Tree.SlotsWithPaths[2].KeyHash, TestItem.Tree.SlotsWithPaths[3].KeyHash });
            _inputStateTree!.Accept(accountProofCollector, _inputStateTree.RootHash);
            proof = accountProofCollector.BuildResult();

            Keccak result2 = SnapProvider.AddStorageRange(store, 1, rootHash, TestItem.Tree.SlotsWithPaths[2].KeyHash, TestItem.Tree.SlotsWithPaths[3..4], proof!.StorageProofs![0].Proof!.Concat(proof!.StorageProofs![1].Proof!).ToArray());

            accountProofCollector = new(TestItem.Tree.AccountAddress0.Bytes, new Keccak[] { TestItem.Tree.SlotsWithPaths[4].KeyHash, TestItem.Tree.SlotsWithPaths[5].KeyHash });
            _inputStateTree!.Accept(accountProofCollector, _inputStateTree.RootHash);
            proof = accountProofCollector.BuildResult();

            Keccak result3 = SnapProvider.AddStorageRange(store, 1, rootHash, TestItem.Tree.SlotsWithPaths[4].KeyHash, TestItem.Tree.SlotsWithPaths[4..6], proof!.StorageProofs![0].Proof!.Concat(proof!.StorageProofs![1].Proof!).ToArray());

            Assert.AreEqual(rootHash, result1);
            Assert.AreNotEqual(rootHash, result2);
            Assert.AreEqual(rootHash, result3);
        }
    }
}