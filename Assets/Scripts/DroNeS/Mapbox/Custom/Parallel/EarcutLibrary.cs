using System;
using System.Collections.Generic;
using DroNeS.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace DroNeS.Mapbox.Custom.Parallel
{

    public struct Data
    {
        public NativeList<float> Vertices;
        public NativeList<int> Holes;
        public int Dim;

        public static Data Get()
        {
            return new Data
            {
                Holes = new NativeList<int>(Allocator.Temp),
                Dim = 2
            };
        }
    }

    public struct Node
    {
        /* Member Variables. */
        // ReSharper disable InconsistentNaming
        public int i;
        public float x;
        public float y;
        public int mZOrder;
        public Bool steiner;
        public unsafe Node* prev;
        public unsafe Node* next;
        public unsafe Node* prevZ;
        public unsafe Node* nextZ;
        public int location;
        // ReSharper restore InconsistentNaming

        public static Node Null => new Node(int.MinValue, float.MinValue, float.MinValue, int.MinValue);
        
        public unsafe Node(int ind, float pX, float pY, int locIdx)
        {
            /* Initialize Member Variables. */
            i = ind;
            x = pX;
            y = pY;
            location = locIdx;
            mZOrder = 0;
            prev = null;
            next = null;
            prevZ = null;
            nextZ = null;
            steiner = false;
        }

        public bool IsNull() => i == int.MinValue && x == float.MinValue && y == float.MinValue;
    }

    public unsafe struct EarcutStruct
    {
        private struct Comparer : IComparer<int>
        {
            private readonly NativeList<Node> _heap;

            public Comparer(ref NativeList<Node> heap)
            {
                _heap = heap;
            }
            public int Compare(int x, int y)
            {
                return (int) math.ceil(_heap[x].x - _heap[y].x);
            }
        }
        
        private NativeList<Node> _heap;
        private Data _result;

        public static NativeList<int> Earcut(NativeList<UnsafeListContainer> data)
        {
            var earcut = new EarcutStruct(data);

            return earcut.Earcut();
        }

        private EarcutStruct(NativeList<UnsafeListContainer> data)
        {
            _heap = new NativeList<Node>(Allocator.Temp);
            _result = default;
            Flatten(data);
        }

        private NativeList<int> Earcut()
        {
            var hasHoles = _result.Holes.Length;
            var outerLen = hasHoles > 0 ? _result.Holes[0] * _result.Dim : _result.Vertices.Length;

            var outerNode = LinkedList(0, outerLen, true);
            
            if (outerNode == null) return new NativeList<int>(3, Allocator.Temp);
            
            var triangles = new NativeList<int>((int) (outerNode->i * 1.5), Allocator.Temp);
            var minX = 0f;
            var minY = 0f;
            var size = 0f;

            if (hasHoles > 0) outerNode = EliminateHoles(outerNode);
            
            if (_result.Vertices.Length > 80 * _result.Dim)
            {
                var maxX = 0f;
                minX = maxX = _result.Vertices[0];
                var maxY = 0f;
                minY = maxY = _result.Vertices[1];

                for (var i = _result.Dim; i < outerLen; i += _result.Dim)
                {
                    var x = _result.Vertices[i];
                    var y = _result.Vertices[i + 1];
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
                
                size = math.max(maxX - minX, maxY - minY);
            }
            EarcutLinked(outerNode, triangles, minX, minY, size);
            _heap.Dispose();
            return triangles;
        }
        
        private void EarcutLinked(Node* ear, NativeList<int> triangles, float minX, float minY, float size)
        {
            var innerEar = ear;
            for (var pass = 0; pass < 3; ++pass) {

                if (innerEar == null) continue;
    
                if (pass == 0 && size > 0) IndexCurve(innerEar, minX, minY, size);

                var stop = innerEar;
        
                while (innerEar->prev != innerEar->next)
                {
                    var prev = innerEar->prev;
                    var next = innerEar->next;

                    if (size > 0 ? IsEarHashed(innerEar, minX, minY, size) : IsEar(innerEar))
                    {
                        // cut off the triangle
                        triangles.Add(prev->i / _result.Dim);
                        triangles.Add(next->i / _result.Dim);
                        triangles.Add(innerEar->i / _result.Dim);

                        RemoveNode(innerEar);

                        // skipping the next vertex leads to less sliver triangles
                        innerEar = next->next;
                        stop = next->next;
                        continue;
                    }

                    innerEar = next;
            
                    if (innerEar != stop) continue;

                    switch (pass)
                    {
                        case 0:
                            innerEar = FilterPoints(innerEar, null);
                            break;
                        case 1:
                            innerEar = CureLocalIntersections(innerEar, triangles);
                            break;
                        case 2:
                            SplitEarcut(innerEar, triangles, minX, minY, size);
                            break;
                    }
                    break;
                }
            }
        }
        

        private void Flatten(NativeList<UnsafeListContainer> data)
        {
            var dataCount = data.Length;
            var totalVertCount = 0;
            for (var i = 0; i < dataCount; i++) totalVertCount += data[i].Length;

            _result = new Data
            {
                Dim = 2, 
                Vertices = new NativeList<float>(totalVertCount * 2, Allocator.Temp),
                Holes =  new NativeList<int>(totalVertCount, Allocator.Temp)
            };
            var holeIndex = 0;

            for (var i = 0; i < dataCount; i++)
            {
                var subCount = data[i].Length;
                for (var j = 0; j < subCount; j++)
                {
                    _result.Vertices.Add(data[i].Get<Vector3>(j)[0]);
                    _result.Vertices.Add(data[i].Get<Vector3>(j)[2]);
                }

                if (i <= 0) continue;
                holeIndex += data[i - 1].Length;
                _result.Holes.Add(holeIndex);
            }
        }
        
        private Node* CureLocalIntersections(Node* start, NativeList<int> triangles)
        {
            var p = start;
            do
            {
                var a = p->prev;
                var b = p->next->next;

                if (!Equals(a, b) && Intersects(a, p, p->next, b) && LocallyInside(a, b) && LocallyInside(b, a))
                {
                    triangles.Add(a->i / _result.Dim);
                    triangles.Add(p->i / _result.Dim);
                    triangles.Add(b->i / _result.Dim);

                    // remove two nodes involved
                    RemoveNode(p);
                    RemoveNode(p->next);

                    p = start = b;
                }

                p = p->next;
            } while (p != start);

            return p;
        }

        private Node* EliminateHoles(Node* outerNode)
        {
            int i;
            var len = _result.Holes.Length;
            var queue = new NativeList<int>(len, Allocator.Temp);
            for (i = 0; i < len; i++)
            {
                var start = _result.Holes[i] * _result.Dim;
                var end = i < len - 1 ? _result.Holes[i + 1] * _result.Dim : _result.Vertices.Length;
                
                var list = LinkedList(start, end, false);
                if (list == list->next) list->steiner = true;
                queue.Add(GetLeftmost(list)->location);
            }
            
            queue.Sort(new Comparer(ref _heap));

            // process holes from left to right
            for (i = 0; i < queue.Length; i++)
            {
                var input = GetNodeAt(queue[i]);
                EliminateHole(input, outerNode);
                outerNode = FilterPoints(outerNode, outerNode->next);
            }

            return outerNode;
        }
        
        private void EliminateHole(Node* hole, Node* outerNode)
        {
            outerNode = FindHoleBridge(hole, outerNode);
            
            if (outerNode == null) return;
            
            var b = SplitPolygon(outerNode, hole);
            FilterPoints(b, b->next);
        }
        
        private Node* SplitPolygon(Node* a, Node* b)
        {
            var a2 = NewNode(a->i, a->x, a->y);
            var b2 = NewNode(b->i, b->x, b->y);
            var an = a->next;
            var bp = b->prev;

            a->next = b;
            b->prev = a;

            a2->next = an;
            an->prev = a2;

            b2->next = a2;
            a2->prev = b2;

            bp->next = b2;
            b2->prev = bp;

            return b2;
        }

        private Node* GetNodeAt(int idx)
        {
            return (Node*) ((IntPtr) _heap.GetUnsafePtr() + idx * sizeof(Node));
        }

        private Node* LinkedList(int start, int end, bool clockwise)
        {
            Node* last = null;

            if (clockwise == (SignedArea(start, end) > 0))
            {
                for (var i = start; i < end; i += _result.Dim) 
                    last = InsertNode(i, _result.Vertices[i], _result.Vertices[i + 1], last);
            }
            else
            {
                for (var i = end - _result.Dim; i >= start; i -= _result.Dim) 
                    last = InsertNode(i, _result.Vertices[i], _result.Vertices[i + 1], last);
            }

            if (last == null || !Equals(last, last->next)) return last;
            RemoveNode(last);
            
            last = last->next;

            return last;
        }

        private float SignedArea(int start, int end)
        {
            var sum = 0f;
            var j = end - _result.Dim;
            for (var i = start; i < end; i += _result.Dim)
            {
                sum += (_result.Vertices[j] - _result.Vertices[i]) * (_result.Vertices[i + 1] + _result.Vertices[j + 1]);
                j = i;
            }

            return sum;
        }

        private Node* NewNode(int i, float x, float y)
        {
            var idx = _heap.Length;
            _heap.Add(new Node(i, x, y, _heap.Length));
            return (Node*) ((IntPtr) _heap.GetUnsafePtr() + (int) ((long) idx * sizeof(Node)));
        }

        private Node* InsertNode(int i, float x, float y, Node* last)
        {
            var ptr = NewNode(i, x, y);

            if (last == null)
            {
                ptr->prev = ptr;
                ptr->next = ptr;
            }
            else
            {
                ptr->next = last->next;
                ptr->prev = last;
                last->next->prev = ptr;
                last->next = ptr;
            }
            return ptr;
        }
        
        private void SplitEarcut(Node* start, NativeList<int> triangles, float minX, float minY, float size)
        {
            var a = start;
            do
            {
                var b = a->next->next;
                while (b != a->prev)
                {
                    if (a->i != b->i && IsValidDiagonal(a, b))
                    {
                        // split the polygon in two by the diagonal
                        var c = SplitPolygon(a, b);

                        // filter co-linear points around the cuts
                        a = FilterPoints(a, a->next);
                        c = FilterPoints(c, c->next);

                        // run earcut on each half
                        EarcutLinked(a, triangles, minX, minY, size);
                        EarcutLinked(c, triangles, minX, minY, size);
                        return;
                    }

                    b = b->next;
                }

                a = a->next;
            } while (a != start);
        }
        
        private static bool IsEar(Node* ear)
        {
            var a = ear->prev;
            var b = ear;
            var c = ear->next;

            if (Area(a, b, c) >= 0) return false; // reflex, can't be an ear

            // now make sure we don't have other points inside the potential ear
            var p = ear->next->next;

            while (p != ear->prev)
            {
                if (PointInTriangle(a->x, a->y, b->x, b->y, c->x, c->y, p->x, p->y) &&
                    Area(p->prev, p,p->next) >= 0) return false;
                p = p->next;
            }

            return true;
        }
        
        private static bool IsEarHashed(Node* ear, float minX, float minY, float size)
        {
            var a = ear->prev;
            var b = ear;
            var c = ear->next;

            if (Area(a, b, c) >= 0) return false; // reflex, can't be an ear

            // triangle bbox; min & max are calculated like this for speed
            var minTx = a->x < b->x ? a->x < c->x ? a->x : c->x : b->x < c->x ? b->x : c->x;
            var minTy = a->y < b->y ? a->y < c->y ? a->y : c->y : b->y < c->y ? b->y : c->y;
            var maxTx = a->x > b->x ? a->x > c->x ? a->x : c->x : b->x > c->x ? b->x : c->x;
            var maxTy = a->y > b->y ? a->y > c->y ? a->y : c->y : b->y > c->y ? b->y : c->y;

            // z-order range for the current triangle bbox;
            var minZ = ZOrder(minTx, minTy, minX, minY, size);
            var maxZ = ZOrder(maxTx, maxTy, minX, minY, size);

            // first look for points inside the triangle in increasing z-order
            var p = ear->nextZ;

            while (p != null && p->mZOrder <= maxZ)
            {
                if (p != ear->prev && p != ear->next &&
                    PointInTriangle(a->x, a->y, b->x, b->y, c->x, c->y, p->x, p->y) &&
                    Area(p->prev, p, p->next) >= 0) return false;
                p = p->nextZ;
            }

            // then look for points in decreasing z-order
            p = ear->prevZ;

            while (p != null && p->mZOrder >= minZ)
            {
                if (p != ear->prev && p != ear->next &&
                    PointInTriangle(a->x, a->y, b->x, b->y, c->x, c->y, p->x, p->y) &&
                    Area(p->prev, p, p->next) >= 0) return false;
                p = p->prevZ;
            }

            return true;
        }
        
        private static void IndexCurve(Node* start, float minX, float minY, float size)
        {
            var p = start;
            do
            {
                if (p->mZOrder == 0) p->mZOrder = ZOrder(p->x, p->y, minX, minY, size);
                p->prevZ = p->prev;
                p->nextZ = p->next;
                p = p->next;
            } while (p != start);

            p->prevZ->nextZ = null;
            p->prevZ = null;

            SortLinked(p);
        }

        private static void SortLinked(Node* list)
        {
            int numMerges;
            var inSize = 1;

            do
            {
                var p = list;
                list = null;
                Node* tail = null;
                numMerges = 0;

                while (p != null)
                {
                    numMerges++;
                    var q = p;
                    var pSize = 0;
                    int i;
                    for (i = 0; i < inSize; i++)
                    {
                        pSize++;
                        q = q->nextZ;
                        if (q == null) break;
                    }

                    var qSize = inSize;

                    while (pSize > 0 || qSize > 0 && q != null)
                    {
                        Node* e;
                        if (pSize != 0 && (qSize == 0 || q == null || p->mZOrder <= q->mZOrder))
                        {
                            e = p;
                            p = p->nextZ;
                            pSize--;
                        }
                        else
                        {
                            e = q;
                            q = q->nextZ;
                            qSize--;
                        }

                        if (tail != null) tail->nextZ = e;
                        else list = e;

                        e->prevZ = tail;
                        tail = e;
                    }

                    p = q;
                }
                tail->nextZ = null;
                inSize *= 2;
            } while (numMerges > 1);
        }
        
        private static int ZOrder(float x, float y, float minX, float minY, float size)
        {
            x = 32767 * (x - minX) / size;
            y = 32767 * (y - minY) / size;

            x = ((int) x | ((int) x << 8)) & 0x00FF00FF;
            x = ((int) x | ((int) x << 4)) & 0x0F0F0F0F;
            x = ((int) x | ((int) x << 2)) & 0x33333333;
            x = ((int) x | ((int) x << 1)) & 0x55555555;

            y = ((int) y | ((int) y << 8)) & 0x00FF00FF;
            y = ((int) y | ((int) y << 4)) & 0x0F0F0F0F;
            y = ((int) y | ((int) y << 2)) & 0x33333333;
            y = ((int) y | ((int) y << 1)) & 0x55555555;

            return (int) x | ((int) y << 1);
        }
        
        private static Node* FindHoleBridge(Node* hole, Node* outerNode)
        {
            var p = outerNode;
            var hx = hole->x;
            var hy = hole->y;
            var qx = float.MinValue;
            Node* m = null;
            
            do
            {
                if (hy <= p->y && hy >= p->next->y && p->next->y != p->y)
                {
                    var x = p->x + (hy - p->y) * (p->next->x - p->x) / (p->next->y - p->y);
                    if (x <= hx && x > qx)
                    {
                        qx = x;
                        if (x == hx)
                        {
                            if (hy == p->y) return p;
                            if (hy == p->next->y) return p->next;
                        }

                        m = p->x < p->next->x ? p : p->next;
                    }
                }
                p = p->next;
            } while (p != outerNode);

            if (m == null) return null;

            if (hx == qx) return m->prev;

            var stop = m;
            var mx = m->x;
            var my = m->y;
            var tanMin = float.MaxValue;

            p = m->next;

            while (p != stop)
            {
                if (hx >= p->x && p->x >= mx && hx != p->x &&
                    PointInTriangle(hy < my ? hx : qx, hy, mx, my, 
                        hy < my ? qx : hx, hy, p->x, p->y))
                {
                    var tan = math.abs(hy - p->y) / (hx - p->x);

                    if ((tan < tanMin || tan == tanMin && p->x > m->x) && LocallyInside(p, hole))
                    {
                        m = p;
                        tanMin = tan;
                    }
                }

                p = p->next;
            }

            return m;
        }

        private static Node* FilterPoints(Node* start, Node* end)
        {
            if (start == null) return null;
            if (end == null) end = start;

            var p = start;
            bool again;
            do
            {
                again = false;

                if (!p->steiner && (Equals(p, p->next) || Area(p->prev, p, p->next) == 0))
                {
                    RemoveNode(p);
                    p = end = p->prev;
                    if (p == p->next) return null;
                    again = true;
                }
                else
                {
                    p = p->next;
                }
            } while (again || p != end);

            return end;
        }

        private static Node* GetLeftmost(Node* start)
        {
            var p = start;
            var leftmost = start;
            do
            {
                if (p->x < leftmost->x) leftmost = p;
                p = p->next;
            } while (p->location != start->location);

            return leftmost;
        }
        
        private static bool LocallyInside(Node* a, Node* b)
        {
            return Area(a->prev, a, a->next) < 0
                ? Area(a, b, a->next) >= 0 && Area(a, a->prev, b) >= 0
                : Area(a, b, a->prev) < 0 || Area(a, a->next, b) < 0;
        }

        private static float Area(Node* p, Node* q, Node* r)
        {
            return (q->y - p->y) * (r->x - q->x) - (q->x - p->x) * (r->y - q->y);
        }

        private static void RemoveNode(Node* p)
        {
            p->next->prev = p->prev;
            p->prev->next = p->next;

            if (p->prevZ != null) p->prevZ->nextZ = p->nextZ;
            if (p->nextZ != null) p->nextZ->prevZ = p->prevZ;
        }

        private static bool Equals(Node* p1, Node* p2)
        {
            return p1->x == p2->x && p1->y == p2->y;
        }
        
        private static bool Intersects(Node* p1, Node* q1, Node* p2, Node* q2)
        {
            if (Equals(p1, q1) && Equals(p2, q2) ||
                Equals(p1, q2) && Equals(p2, q1)) return true;
            return Area(p1, q1, p2) > 0 != Area(p1, q1, q2) > 0 &&
                   Area(p2, q2, p1) > 0 != Area(p2, q2, q1) > 0;
        }
        
        private static bool MiddleInside(Node* a, Node* b)
        {
            var p = a;
            var inside = false;
            var px = (a->x + b->x) / 2;
            var py = (a->y + b->y) / 2;

            do
            {
                if (p->y > py != p->next->y > py && p->next->y != p->y &&
                    px < (p->next->x - p->x) * (py - p->y) / (p->next->y - p->y) + p->x)
                    inside = !inside;
                p = p->next;
            } while (p != a);

            return inside;
        }

        private static bool IntersectsPolygon(Node* a, Node* b)
        {
            var p = a;
            do
            {
                if (p->i != a->i && p->next->i != a->i && p->i != b->i && p->next->i != b->i &&
                    Intersects(p, p->next, a, b)) return true;
                p = p->next;
            } while (p != a);

            return false;
        }
        
        private static bool IsValidDiagonal(Node* a, Node* b)
        {
            return a->next->i != b->i && a->prev->i != b->i && !IntersectsPolygon(a, b) &&
                   LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b);
        }
        
        private static bool PointInTriangle(float ax, float ay, float bx, float by, float cx, float cy, float px,
            float py)
        {
            return (cx - px) * (ay - py) - (ax - px) * (cy - py) >= 0 &&
                   (ax - px) * (by - py) - (bx - px) * (ay - py) >= 0 &&
                   (bx - px) * (cy - py) - (cx - px) * (by - py) >= 0;
        }
    }
}