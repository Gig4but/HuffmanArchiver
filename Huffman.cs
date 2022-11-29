using System;
using System.Collections.Generic;
using System.IO;

namespace Program {
    class Node {
        public byte? value { get; set; }
        public ulong weight { get; set; }
        public Node left { get; set; }
        public Node right { get; set; }
    }

    class HuffmanTree {
        readonly byte[] header = { 0x7B, 0x68, 0x75, 0x7C, 0x6D, 0x7D, 0x66, 0x66 };
        public Node root { get; private set; }
        public Dictionary<byte, (int, byte)> codes { get; private set; } = new Dictionary<byte, (int, byte)>(byte.MaxValue); // Value, Huff code, huff code bit count

        public static HuffmanTree Encode(Stream stream) {
            ulong[] dataset = new ulong[byte.MaxValue + 1];

            // count values
            int b;
            while ((b = stream.ReadByte()) != -1)
                dataset[b]++;

            // make tree
            List<Node> forest = new List<Node>(dataset.Length);
            for (int i = 0; i < dataset.Length; i++)
                if (dataset[i] != 0)
                    forest.Add(new Node { value = (byte)i, weight = dataset[i] });

            while (forest.Count > 1) {
                Node left = PopMin(ref forest);
                Node right = PopMin(ref forest);
                forest.Add(new Node {
                    weight = left.weight + right.weight,
                    left = left,
                    right = right
                });
            }

            stream.Seek(0, SeekOrigin.Begin);

            HuffmanTree tree = new HuffmanTree() { root = forest[0] };
            tree.CreateCodeMap(tree.root);

            return tree;
        }

        static Node PopMin(ref List<Node> forest) {
            (Node node, int i) min = (forest[0], 0);
            for (int i = 1; i < forest.Count; i++) {
                if (forest[i].weight < min.node.weight)
                    min = (forest[i], i);
                else if (min.node.value == null && forest[i].value != null)
                    min = (forest[i], i);
                else if (min.node.value != null && forest[i].value != null
                    && forest[i].value < min.node.value)
                    min = (forest[i], i);
            }
            forest.RemoveAt(min.i);
            return min.node;
        }

        void CreateCodeMap(Node node, int code = 0, int bitCount = 0) {
            if (node.left != null)
                CreateCodeMap(node.left, (code << 1), bitCount + 1);
            if (node.right != null)
                CreateCodeMap(node.right, (code << 1) + 1, bitCount + 1);
            if (node.value != null)
                codes.Add(node.value.Value, (code, (byte)bitCount));
        }

        public void Export(Stream input, BinaryWriter output) {
            output.Write(header);

            // write tree
            Stack<Node> stack = new Stack<Node>();
            stack.Push(root);
            Node node;
            ulong binaryNode;
            while (stack.Count > 0) {
                node = stack.Pop();
                binaryNode = 0;

                if (node.value == null) {
                    stack.Push(node.right);
                    stack.Push(node.left);
                } else {
                    binaryNode |= 0x1; // set last bit to 1
                    binaryNode |= (ulong)node.value << 56; // set value as first 8 bits
                }
                binaryNode |= (node.weight << 1) & 0x00FFFFFFFFFFFFFE; // set only last 55bits of weight after 8 bits
                output.Write(binaryNode);
            }
            output.Write((ulong)0);

            //write data
            int b;
            int offset = 0;
            int sequence = 0;
            int reverseHuff = 0;
            (int huff, byte bits) code;
            while ((b = input.ReadByte()) != -1) {
                code = codes[(byte)b];
                reverseHuff = 0;
                for (int i = 0; i < code.bits; i++) {
                    reverseHuff |= (((code.huff & (1 << i)) >> i) & 1) << (code.bits - 1 - i);
                }
                sequence |= (reverseHuff) << offset;
                offset += code.bits;

                while (offset > 7) {
                    output.Write((byte)sequence);
                    sequence >>= 8;
                    offset -= 8;
                }
            }
            if (offset > 0)
                output.Write((byte)sequence);
        }
    }

    class Program {
        public static void Main(string[] args) {
            if (args.Length != 1) {
                Console.WriteLine("Argument Error");
                return;
            }
    
            try {
                using (FileStream input = File.OpenRead(args[0]), output = File.OpenWrite(args[0] + ".huff")) {
                    if (input.Length == 0)
                        return;
                    HuffmanTree.Encode(input).Export(input, new BinaryWriter(output));
                }
            }
            catch {
                Console.WriteLine("File Error");
                return;
            }
        }
    }
}