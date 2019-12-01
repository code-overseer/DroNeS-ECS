using System;
using System.Collections.Generic;
using DroNeS.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace DroNeS.Mapbox.JobSystem
{
    public static unsafe class EarcutLibrary
    {
        public static NativeList<int> Earcut(NativeList<float> data, NativeList<int> holeIndices, int dim, ref NativeList<Node> job)
        {
            dim = math.max(dim, 2);

            var hasHoles = holeIndices.Length;
            var outerLen = hasHoles > 0 ? holeIndices[0] * dim : data.Length;

            var outerNode = LinkedList(data, 0, outerLen, dim, true, ref job);
            
            if (outerNode == null) return new NativeList<int>(3, Allocator.Temp);
            
            var triangles = new NativeList<int>((int) (outerNode->i * 1.5), Allocator.Temp);
            var minX = 0f;
            var minY = 0f;
            var size = 0f;

            if (hasHoles > 0) outerNode = EliminateHoles(data, holeIndices, outerNode, dim, ref job);

            // if the shape is not too simple, we'll use z-order curve hash later; calculate polygon bbox
            if (data.Length > 80 * dim)
            {
                var maxX = 0f;
                minX = maxX = data[0];
                var maxY = 0f;
                minY = maxY = data[1];

                for (var i = dim; i < outerLen; i += dim)
                {
                    var x = data[i];
                    var y = data[i + 1];
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }

                // minX, minY and size are later used to transform coords into integers for z-order calculation
                size = math.max(maxX - minX, maxY - minY);
            }

            EarcutLinked(outerNode, triangles, dim, minX, minY, size, ref job);

            return triangles;
        }

        private static void EarcutLinked(Node* ear, NativeList<int> triangles, int dim, float minX, float minY, float size, ref NativeList<Node> job,
            int pass = 0)
        {
            if (ear == null) return;

            // interlink polygon nodes in z-order
            if (pass == 0 && size > 0) IndexCurve(ear, minX, minY, size);

            var stop = ear;

            // iterate through ears, slicing them one by one
            while (ear->prev != ear->next)
            {
                var prev = ear->prev;
                var next = ear->next;

                if (size > 0 ? IsEarHashed(ear, minX, minY, size) : IsEar(ear))
                {
                    // cut off the triangle
                    triangles.Add(prev->i / dim);
                    triangles.Add(next->i / dim);
                    triangles.Add(ear->i / dim);

                    RemoveNode(ear, ref job);

                    // skipping the next vertice leads to less sliver triangles
                    ear = next->next;
                    stop = next->next;

                    continue;
                }

                ear = next;

                // if we looped through the whole remaining polygon and can't find any more ears
                if (ear != stop) continue;
                switch (pass)
                {
                    // try filtering points and slicing again
                    case 0:
                        EarcutLinked(FilterPoints(ear, null, ref job), triangles, dim, minX, minY, size, ref job, 1);

                        // if this didn't work, try curing all small self-intersections locally
                        break;
                    case 1:
                        ear = CureLocalIntersections(ear, triangles, dim, ref job);
                        EarcutLinked(ear, triangles, dim, minX, minY, size, ref job, 2);

                        // as a last resort, try splitting the remaining polygon into two
                        break;
                    case 2:
                        SplitEarcut(ear, triangles, dim, minX, minY, size, ref job);
                        break;
                }

                break;
            }
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

        private static int ZOrder(float x, float y, float minX, float minY, float size)
        {
            //TODO casting here might be wrong
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

        private static void SplitEarcut(Node* start, NativeList<int> triangles, int dim, float minX, float minY, float size, ref NativeList<Node> job)
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
                        var c = SplitPolygon(a, b, ref job);

                        // filter colinear points around the cuts
                        a = FilterPoints(a, a->next, ref job);
                        c = FilterPoints(c, c->next, ref job);

                        // run earcut on each half
                        EarcutLinked(a, triangles, dim, minX, minY, size, ref job);
                        EarcutLinked(c, triangles, dim, minX, minY, size, ref job);
                        return;
                    }

                    b = b->next;
                }

                a = a->next;
            } while (a != start);
        }

        private static bool IsValidDiagonal(Node* a, Node* b)
        {
            return a->next->i != b->i && a->prev->i != b->i && !IntersectsPolygon(a, b) &&
                   LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b);
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

        private static Node* CureLocalIntersections(Node* start, NativeList<int> triangles, int dim, ref NativeList<Node> job)
        {
            var p = start;
            do
            {
                var a = p->prev;
                var b = p->next->next;

                if (!Equals(a, b) && Intersects(a, p, p->next, b) && LocallyInside(a, b) && LocallyInside(b, a))
                {
                    triangles.Add(a->i / dim);
                    triangles.Add(p->i / dim);
                    triangles.Add(b->i / dim);

                    // remove two nodes involved
                    RemoveNode(p, ref job);
                    RemoveNode(p->next, ref job);

                    p = start = b;
                }

                p = p->next;
            } while (p != start);

            return p;
        }

        private static bool Intersects(Node* p1, Node* q1, Node* p2, Node* q2)
        {
            if (Equals(p1, q1) && Equals(p2, q2) ||
                Equals(p1, q2) && Equals(p2, q1)) return true;
            return Area(p1, q1, p2) > 0 != Area(p1, q1, q2) > 0 &&
                   Area(p2, q2, p1) > 0 != Area(p2, q2, q1) > 0;
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

        private struct Comparer : IComparer<int>
        {
            private readonly NativeList<Node> _job;

            public Comparer(ref NativeList<Node> job)
            {
                _job = job;
            }
            public int Compare(int x, int y)
            {
                return (int) math.ceil(_job[x].x - _job[y].x);
            }
        }

        private static Node* EliminateHoles(NativeList<float> data, NativeList<int> holeIndices, Node* outerNode, int dim, ref NativeList<Node> job)
        {
            int i;
            var len = holeIndices.Length;
            var queue = new NativeList<int>(len, Allocator.Temp);
            for (i = 0; i < len; i++)
            {
                var start = holeIndices[i] * dim;
                var end = i < len - 1 ? holeIndices[i + 1] * dim : data.Length;
                var list = LinkedList(data, start, end, dim, false, ref job);
                if (list == list->next) list->steiner = true;
                queue.Add(GetLeftmost(list)->location);
            }
            
            queue.Sort(new Comparer(ref job));

            // process holes from left to right
            for (i = 0; i < queue.Length; i++)
            {
                var input = (Node*) ((IntPtr) job.GetUnsafePtr() + (int) ((long) queue[i] * sizeof(Node)));
                EliminateHole(input, outerNode, ref job);
                outerNode = FilterPoints(outerNode, outerNode->next, ref job);
            }

            return outerNode;
        }

        private static void EliminateHole(Node* hole, Node* outerNode, ref NativeList<Node> job)
        {
            outerNode = FindHoleBridge(hole, outerNode);
            
            if (outerNode == null) return;
            
            var b = SplitPolygon(outerNode, hole, ref job);
            FilterPoints(b, b->next, ref job);
        }

        private static Node* FilterPoints(Node* start, Node* end, ref NativeList<Node> job)
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
                    RemoveNode(p, ref job);
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

        private static Node* SplitPolygon(Node* a, Node* b, ref NativeList<Node> job)
        {
            var a2 = *a;
            a2.location = job.Length;
            job.Add(a2);
            var b2 = *b;
            b2.location = job.Length;
            job.Add(b2);


            var aCopy = (Node*) ((IntPtr) job.GetUnsafePtr() + (int)((long) a2.location * sizeof(Node)));
            var bCopy = (Node*) ((IntPtr) job.GetUnsafePtr() + (int)((long) b2.location * sizeof(Node)));
            
            var an = a->next;
            var bp = b->prev;

            a->next = b;
            b->prev = a;

            a2.next = an;
            an->prev = aCopy;

            b2.next = aCopy;
            a2.prev = bCopy;

            bp->next = bCopy;
            b2.prev = bp;

            return bCopy;
        }

        private static Node* FindHoleBridge(Node* hole, Node* outerNode)
        {
            var p = outerNode;
            var hx = hole->x;
            var hy = hole->y;
            var qx = float.MinValue;
            Node* m = null;

            // find a segment intersected by a ray from the hole's leftmost point to the left;
            // segment's endpoint with lesser x will be potential connection point
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

            if (hx == qx) return m->prev; // hole touches outer segment; pick lower endpoint

            // look for points inside the triangle of hole point, segment intersection and endpoint;
            // if there are no points found, we have a valid connection;
            // otherwise choose the point of the minimum angle with the ray as connection point

            var stop = m;
            var mx = m->x;
            var my = m->y;
            var tanMin = float.MaxValue;

            p = m->next;

            while (p != stop)
            {
                if (hx >= p->x && p->x >= mx && hx != p->x &&
                    PointInTriangle(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, p->x, p->y))
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

        private static bool PointInTriangle(float ax, float ay, float bx, float by, float cx, float cy, float px,
            float py)
        {
            return (cx - px) * (ay - py) - (ax - px) * (cy - py) >= 0 &&
                   (ax - px) * (by - py) - (bx - px) * (ay - py) >= 0 &&
                   (bx - px) * (cy - py) - (cx - px) * (by - py) >= 0;
        }

        private static Node* GetLeftmost(Node* start)
        {
            var p = start;
            var leftmost = start;
            do
            {
                if (p->x < leftmost->x) leftmost = p;
                p = p->next;
            } while (p != start);

            return leftmost;
        }

        // create a circular doubly linked list from polygon points in the specified winding order
        private static Node* LinkedList(NativeList<float> data, int start, int end, int dim, bool clockwise, ref NativeList<Node> job)
        {
            int i;
            Node* last = null;

            if (clockwise == (SignedArea(data, start, end, dim) > 0))
            {
                for (i = start; i < end; i += dim) last = InsertNode(i, data[i], data[i + 1], last, ref job);
            }
            else
            {
                for (i = end - dim; i >= start; i -= dim) last = InsertNode(i, data[i], data[i + 1], last, ref job);
            }

            if (last == null || !Equals(last, last->next)) return last;
            RemoveNode(last, ref job);
            
            last = last->next;

            return last;
        }

        private static void RemoveNode(Node* p, ref NativeList<Node> job)
        {
            p->next->prev = p->prev;
            p->prev->next = p->next;

            if (p->prevZ != null) p->prevZ->nextZ = p->nextZ;
            if (p->nextZ != null) p->nextZ->prevZ = p->prevZ;

            var location = p->location;
            job.RemoveAtSwapBack(location);
            var tmp = (Node*) ((IntPtr) job.GetUnsafePtr() + (int) ((long) location * sizeof(Node)));
            tmp->location = location;
        }

        private static bool Equals(Node* p1, Node* p2)
        {
            return p1->x == p2->x && p1->y == p2->y;
        }

        private static float SignedArea(NativeList<float> data, int start, int end, int dim)
        {
            var sum = 0f;
            var j = end - dim;
            for (var i = start; i < end; i += dim)
            {
                sum += (data[j] - data[i]) * (data[i + 1] + data[j + 1]);
                j = i;
            }

            return sum;
        }

        private static Node* InsertNode(int i, float x, float y, Node* last, ref NativeList<Node> job)
        {
            var idx = job.Length;
            job.Add(new Node(i, x, y, job.Length));
            var ptr = (Node*) ((IntPtr) job.GetUnsafePtr() + (int) ((long) idx * sizeof(Node)));

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
        
        public static Data Flatten(NativeList<UnsafeListContainer> data)
        {
            var dataCount = data.Length;
            var totalVertCount = 0;
            for (var i = 0; i < dataCount; i++)
            {
                totalVertCount += data[i].Length;
            }

            var result = new Data
            {
                Dim = 2, 
                Vertices = new NativeList<float>(totalVertCount * 2, Allocator.Temp)
            };
            var holeIndex = 0;

            for (var i = 0; i < dataCount; i++)
            {
                var subCount = data[i].Length;
                for (var j = 0; j < subCount; j++)
                {
                    result.Vertices.Add(data[i].Get<Vector3>(j)[0]);
                    result.Vertices.Add(data[i].Get<Vector3>(j)[2]);
                }

                if (i <= 0) continue;
                holeIndex += data[i - 1].Length;
                result.Holes.Add(holeIndex);
            }
            return result;
        }
        
    }

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
}