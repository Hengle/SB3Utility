﻿using System;
using System.Collections.Generic;
using System.Text;
using SlimDX;

namespace SB3Utility
{
	[Plugin]
	public class xaEditor : IDisposable
	{
		public xaParser Parser { get; protected set; }

		public xaEditor(xaParser parser)
		{
			Parser = parser;
		}

		public void Dispose()
		{
			Parser = null;
		}

		[Plugin]
		public void ReplaceMorph(WorkspaceMorph morph, string destMorphName, string newName, bool replaceNormals, double minSquaredDistance)
		{
			xa.ReplaceMorph(destMorphName, Parser, morph, newName, replaceNormals, (float)minSquaredDistance);
		}

		[Plugin]
		public void CalculateNormals(xxFrame meshFrame, string morphClip, string keyframe, double threshold)
		{
			xa.CalculateNormals(Parser, meshFrame, morphClip, keyframe, (float)threshold);
		}

		[Plugin]
		public void CreateMorphKeyframeRef(string morphClip, int position, string keyframe)
		{
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				if (clip.Name == morphClip)
				{
					xaMorphKeyframeRef morphRef = new xaMorphKeyframeRef();
					xa.CreateUnknowns(morphRef);
					morphRef.Index = -1;
					morphRef.Name = keyframe;
					clip.KeyframeRefList.Insert(position, morphRef);
					return;
				}
			}
		}

		[Plugin]
		public void RemoveMorphKeyframeRef(string morphClip, int position)
		{
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				if (clip.Name == morphClip)
				{
					clip.KeyframeRefList.RemoveAt(position);
					return;
				}
			}
		}

		[Plugin]
		public void MoveMorphKeyframeRef(string morphClip, int fromPos, int toPos)
		{
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				if (clip.Name == morphClip)
				{
					xaMorphKeyframeRef morphRef = clip.KeyframeRefList[fromPos];
					clip.KeyframeRefList.RemoveAt(fromPos);
					clip.KeyframeRefList.Insert(toPos, morphRef);
					return;
				}
			}
		}

		[Plugin]
		public void RemoveMorphKeyframe(string name)
		{
			xaMorphKeyframe keyframe = xa.FindMorphKeyFrame(name, Parser.MorphSection);
			Parser.MorphSection.KeyframeList.Remove(keyframe);
		}

		[Plugin]
		public void SetMorphKeyframeRefKeyframe(string morphClip, int position, string keyframe)
		{
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				if (clip.Name == morphClip)
				{
					clip.KeyframeRefList[position].Name = keyframe;
					return;
				}
			}
		}

		[Plugin]
		public void SetMorphKeyframeRefIndex(string morphClip, int position, int id)
		{
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				if (clip.Name == morphClip)
				{
					clip.KeyframeRefList[position].Index = id;
					return;
				}
			}
		}

		[Plugin]
		public void RenameMorphKeyframe(int position, string newName)
		{
			xaMorphKeyframe keyframe = Parser.MorphSection.KeyframeList[position];
			string oldName = keyframe.Name;
			foreach (xaMorphClip clip in Parser.MorphSection.ClipList)
			{
				foreach (xaMorphKeyframeRef morphRef in clip.KeyframeRefList)
				{
					if (morphRef.Name == oldName)
					{
						morphRef.Name = newName;
					}
				}
			}
			keyframe.Name = newName;
		}

		[Plugin]
		public void ReplaceAnimation(WorkspaceAnimation animation, int resampleCount, string method, int insertPos)
		{
			var replaceMethod = (ReplaceAnimationMethod)Enum.Parse(typeof(ReplaceAnimationMethod), method);
			ReplaceAnimation(animation, Parser, resampleCount, replaceMethod, insertPos);
		}

		public static void ReplaceAnimation(WorkspaceAnimation wsAnimation, xaParser parser, int resampleCount, ReplaceAnimationMethod replaceMethod, int insertPos)
		{
			if (parser.AnimationSection == null)
			{
				Report.ReportLog("The .xa file doesn't have an animation section. Skipping this animation");
				return;
			}

			Report.ReportLog("Replacing animation ...");
			List<KeyValuePair<string, xaAnimationKeyframe[]>> newTrackList = new List<KeyValuePair<string, xaAnimationKeyframe[]>>(wsAnimation.TrackList.Count);
			List<Tuple<ImportedAnimationTrack, xaAnimationKeyframe[]>> interpolateTracks = new List<Tuple<ImportedAnimationTrack,xaAnimationKeyframe[]>>();
			foreach (var wsTrack in wsAnimation.TrackList)
			{
				if (!wsAnimation.isTrackEnabled(wsTrack))
					continue;
				xaAnimationKeyframe[] newKeyframes = new xaAnimationKeyframe[resampleCount];
				int wsTrackKeyframesLength = 0;
				for (int i = 0; i < wsTrack.Keyframes.Length; i++)
				{
					if (wsTrack.Keyframes[i] != null)
						wsTrackKeyframesLength++;
				}
				if (wsTrackKeyframesLength == resampleCount)
				{
					for (int i = 0; i < wsTrack.Keyframes.Length; i++)
					{
						ImportedAnimationKeyframe keyframe = wsTrack.Keyframes[i];
						if (keyframe == null)
							continue;

						newKeyframes[i] = new xaAnimationKeyframe();
						newKeyframes[i].Index = i;
						newKeyframes[i].Rotation = keyframe.Rotation;
						xa.CreateUnknowns(newKeyframes[i]);
						newKeyframes[i].Translation = keyframe.Translation;
						newKeyframes[i].Scaling = keyframe.Scaling;
					}
				}
				else
				{
					if (wsTrackKeyframesLength < 1)
					{
						xaAnimationKeyframe keyframe = new xaAnimationKeyframe();
						keyframe.Rotation = Quaternion.Identity;
						keyframe.Scaling = new Vector3(1, 1, 1);
						keyframe.Translation = new Vector3(0, 0, 0);
						xa.CreateUnknowns(keyframe);

						for (int i = 0; i < newKeyframes.Length; i++)
						{
							keyframe.Index = i;
							newKeyframes[i] = keyframe;
						}
					}
					else
					{
						interpolateTracks.Add(new Tuple<ImportedAnimationTrack, xaAnimationKeyframe[]>(wsTrack, newKeyframes));
					}
				}

				newTrackList.Add(new KeyValuePair<string, xaAnimationKeyframe[]>(wsTrack.Name, newKeyframes));
			}
			Fbx.InterpolateKeyframes(interpolateTracks, resampleCount);

			List<xaAnimationTrack> animationNodeList = parser.AnimationSection.TrackList;
			Dictionary<string, xaAnimationTrack> animationNodeDic = null;
			if (replaceMethod != ReplaceAnimationMethod.Replace)
			{
				animationNodeDic = new Dictionary<string, xaAnimationTrack>();
				foreach (xaAnimationTrack animationNode in animationNodeList)
				{
					animationNodeDic.Add(animationNode.Name, animationNode);
				}
			}

			if (replaceMethod == ReplaceAnimationMethod.Replace)
			{
				animationNodeList.Clear();
				foreach (var newTrack in newTrackList)
				{
					xaAnimationTrack animationNode = new xaAnimationTrack();
					animationNodeList.Add(animationNode);
					animationNode.KeyframeList = new List<xaAnimationKeyframe>(newTrack.Value);
					animationNode.Name = newTrack.Key;
					xa.CreateUnknowns(animationNode);
				}
			}
			else if (replaceMethod == ReplaceAnimationMethod.Merge)
			{
				foreach (var newTrack in newTrackList)
				{
					xaAnimationTrack animationNode;
					xaAnimationKeyframe[] origKeyframes = xa.animationGetOriginalKeyframes(animationNodeDic, newTrack.Key, animationNodeList, out animationNode);
					xaAnimationKeyframe[] destKeyframes;
					int newEnd = insertPos + newTrack.Value.Length;
					if (origKeyframes.Length < insertPos)
					{
						destKeyframes = new xaAnimationKeyframe[newEnd];
						xa.animationCopyKeyframeTransformArray(origKeyframes, 0, destKeyframes, 0, origKeyframes.Length);
						xa.animationNormalizeTrack(origKeyframes, destKeyframes, insertPos);
					}
					else
					{
						if (origKeyframes.Length < newEnd)
						{
							destKeyframes = new xaAnimationKeyframe[newEnd];
						}
						else
						{
							destKeyframes = new xaAnimationKeyframe[origKeyframes.Length];
							xa.animationCopyKeyframeTransformArray(origKeyframes, newEnd, destKeyframes, newEnd, origKeyframes.Length - newEnd);
						}
						xa.animationCopyKeyframeTransformArray(origKeyframes, 0, destKeyframes, 0, insertPos);
					}

					xa.animationCopyKeyframeTransformArray(newTrack.Value, 0, destKeyframes, insertPos, newTrack.Value.Length);
					animationNode.KeyframeList = new List<xaAnimationKeyframe>(destKeyframes);
				}
			}
			else if (replaceMethod == ReplaceAnimationMethod.Insert)
			{
				foreach (var newTrack in newTrackList)
				{
					xaAnimationTrack animationNode;
					xaAnimationKeyframe[] origKeyframes = xa.animationGetOriginalKeyframes(animationNodeDic, newTrack.Key, animationNodeList, out animationNode); ;
					xaAnimationKeyframe[] destKeyframes;
					int newEnd = insertPos + newTrack.Value.Length;
					if (origKeyframes.Length < insertPos)
					{
						destKeyframes = new xaAnimationKeyframe[newEnd];
						xa.animationCopyKeyframeTransformArray(origKeyframes, 0, destKeyframes, 0, origKeyframes.Length);
						xa.animationNormalizeTrack(origKeyframes, destKeyframes, insertPos);
					}
					else
					{
						destKeyframes = new xaAnimationKeyframe[origKeyframes.Length + newTrack.Value.Length];
						xa.animationCopyKeyframeTransformArray(origKeyframes, 0, destKeyframes, 0, insertPos);
						xa.animationCopyKeyframeTransformArray(origKeyframes, insertPos, destKeyframes, newEnd, origKeyframes.Length - insertPos);
					}

					xa.animationCopyKeyframeTransformArray(newTrack.Value, 0, destKeyframes, insertPos, newTrack.Value.Length);
					animationNode.KeyframeList = new List<xaAnimationKeyframe>(destKeyframes);
				}
			}
			else if (replaceMethod == ReplaceAnimationMethod.Append)
			{
				int maxKeyframes = 0;
				foreach (xaAnimationTrack animationNode in animationNodeList)
				{
					int numKeyframes = animationNode.KeyframeList.Count;
					if (numKeyframes > maxKeyframes)
					{
						maxKeyframes = numKeyframes;
					}
				}

				foreach (var newTrack in newTrackList)
				{
					xaAnimationTrack animationNode;
					xaAnimationKeyframe[] origKeyframes = xa.animationGetOriginalKeyframes(animationNodeDic, newTrack.Key, animationNodeList, out animationNode);
					xaAnimationKeyframe[] destKeyframes = new xaAnimationKeyframe[maxKeyframes + newTrack.Value.Length];
					xa.animationCopyKeyframeTransformArray(origKeyframes, 0, destKeyframes, 0, origKeyframes.Length);
					if (origKeyframes.Length < maxKeyframes)
					{
						xa.animationNormalizeTrack(origKeyframes, destKeyframes, maxKeyframes);
					}

					xa.animationCopyKeyframeTransformArray(newTrack.Value, 0, destKeyframes, maxKeyframes, newTrack.Value.Length);
					animationNode.KeyframeList = new List<xaAnimationKeyframe>(destKeyframes);
				}
			}
			else
			{
				Report.ReportLog("Error: Unexpected animation replace method " + replaceMethod + ". Skipping this animation");
				return;
			}
		}
	}
}
