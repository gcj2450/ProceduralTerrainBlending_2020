﻿using UnityEngine;

public class CullingGroupBehaviour : MonoBehaviour
{
	// just some dummy prefab to spawn (use default sphere for example)
	public GameObject prefab;

	// distance to search objects from
	public float searchDistance = 200;

	public bool colorInvisibleObjects = false;

	int objectCount = 200;

	// collection of objects
	Renderer[] objects;
	CullingGroup cullGroup;
	BoundingSphere[] bounds;

	public Camera m_camera;

	void Start()
	{
		// create culling group
		cullGroup = new CullingGroup();
		cullGroup.targetCamera = m_camera;

		// measure distance to our transform
		cullGroup.SetDistanceReferencePoint(m_camera.transform);

		// search distance "bands" starts from 0, so index=0 is from 0 to searchDistance
		cullGroup.SetBoundingDistances(new float[] { searchDistance, float.PositiveInfinity });

		bounds = new BoundingSphere[objectCount];

		// spam random objects
		objects = new Renderer[objectCount];
		for (int i = 0; i < objectCount; i++)
		{
			var pos = Camera.main.transform.position + Random.insideUnitSphere * 100 + new Vector3 (0,-100,0);
			var go = Instantiate(prefab, pos, Quaternion.identity);
			objects[i] = go.GetComponent<Renderer>();

			// collect bounds for objects
			var b = new BoundingSphere();
			b.position = go.transform.position;

			// get simple radius..works for our sphere
			b.radius = go.GetComponent<MeshFilter>().mesh.bounds.extents.x;
			bounds[i] = b;
		}

		// set bounds that we track
		cullGroup.SetBoundingSpheres(bounds);
		cullGroup.SetBoundingSphereCount(objects.Length);

		// subscribe to event
		cullGroup.onStateChanged += StateChanged;
	}

	void Update()
	{
		cullGroup.SetDistanceReferencePoint(m_camera.transform);
	}


	// object state has changed in culling group
	void StateChanged(CullingGroupEvent e)
	{
		if (colorInvisibleObjects == true && e.isVisible == false)
		{
			objects[e.index].material.color = Color.gray;
			return;
		}

		// if we are in distance band index 0, that is between 0 to searchDistance
		if (e.currentDistance == 0)
		{
			//objects[e.index].enabled = true;
			objects[e.index].material.color = Color.green;
		}
		else // too far, set color to red
		{
			//objects[e.index].enabled = false;
			objects[e.index].material.color = Color.red;
		}
	}

	// cleanup
	private void OnDestroy()
	{
		if(cullGroup !=null)
		{
			cullGroup.onStateChanged -= StateChanged;
			cullGroup.Dispose();
			cullGroup = null;
		}
		
	}

}

